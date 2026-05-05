using Microsoft.EntityFrameworkCore;
using ShippingOrchestrator.Domain.Ingestion;
using ShippingOrchestrator.Infrastructure.Persistence;

namespace ShippingOrchestrator.Worker.Jobs;

/// <summary>
/// Background service that periodically deletes Resolved/Dismissed ingestion failures whose
/// 30-day TTL has elapsed (<see cref="IngestionFailure.ExpiresAt"/>). Open rows are kept
/// indefinitely so a long-running issue stays surfaced. Read-side projections are NOT touched
/// here — v1 retains them in <c>customer_read.ingestion_failures</c> and
/// <c>ops_read.ingestion_failures</c> for analytics.
/// </summary>
public sealed class IngestionFailurePurgeService : BackgroundService
{
    private static readonly TimeSpan PurgeInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);
    private const int BatchSize = 500;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IngestionFailurePurgeService> _log;

    public IngestionFailurePurgeService(IServiceScopeFactory scopeFactory, ILogger<IngestionFailurePurgeService> log)
    {
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Defer first run so the Worker isn't competing with EF migrations + Wolverine
        // start-up traffic on cold boot. Cancellable so SIGTERM during boot is clean.
        try
        {
            await Task.Delay(StartupDelay, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(PurgeInterval);
        do
        {
            try
            {
                await PurgeOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                _log.LogError(ex, "Ingestion failure purge run failed; will retry on next tick.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task PurgeOnceAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>();

        // Hold a single Postgres connection so the session-level advisory lock survives across
        // every batched delete in this run. Without this, EF would acquire and release a fresh
        // connection per query and the lock would dangle on the first connection.
        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        try
        {
            if (!await TryAcquireLeaseAsync(db, ct).ConfigureAwait(false))
            {
                _log.LogDebug("Skipping ingestion-failure purge: another worker holds the lease.");
                return;
            }
            try
            {
                await PurgeBatchesAsync(db, ct).ConfigureAwait(false);
            }
            finally
            {
                await ReleaseLeaseAsync(db, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }

    private async Task PurgeBatchesAsync(OrchestratorDbContext db, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var totalDeleted = 0;

        while (!ct.IsCancellationRequested)
        {
            var batch = await db.IngestionFailures
                .Where(f => (f.Status == IngestionFailureStatus.Resolved
                              || f.Status == IngestionFailureStatus.Dismissed)
                            && f.ExpiresAt != null
                            && f.ExpiresAt < now)
                .OrderBy(f => f.ExpiresAt)
                .Take(BatchSize)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            if (batch.Count == 0) break;

            db.IngestionFailures.RemoveRange(batch);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            totalDeleted += batch.Count;

            if (batch.Count < BatchSize) break;
        }

        if (totalDeleted > 0)
            _log.LogInformation("Purged {Count} expired ingestion failures.", totalDeleted);
    }

    private static async Task<bool> TryAcquireLeaseAsync(OrchestratorDbContext db, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT pg_try_advisory_lock({MigrationLockKeys.IngestionFailurePurge})";
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is bool b && b;
    }

    private static async Task ReleaseLeaseAsync(OrchestratorDbContext db, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT pg_advisory_unlock({MigrationLockKeys.IngestionFailurePurge})";
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
