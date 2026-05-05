using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ShippingOrchestrator.Infrastructure.Persistence;

/// <summary>
/// Wraps EF <see cref="DatabaseFacade.MigrateAsync"/> in a Postgres session-level advisory lock
/// so multi-replica deployments don't race on <c>__ef_migrations_history</c>. Exactly one replica
/// per <paramref name="lockKey"/> applies migrations; others wait on the lock, then their own
/// <c>MigrateAsync</c> becomes a no-op once the history table is up to date.
/// </summary>
public static class MigrationCoordinator
{
    public static async Task RunWithAdvisoryLockAsync(
        DbContext db,
        long lockKey,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);

        await db.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            logger.LogInformation(
                "Acquiring migration advisory lock {LockKey} for {Context}",
                lockKey, db.GetType().Name);

            await ExecuteScalarAsync(db, "SELECT pg_advisory_lock({0})", lockKey, cancellationToken)
                .ConfigureAwait(false);
            try
            {
                await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await ExecuteScalarAsync(db, "SELECT pg_advisory_unlock({0})", lockKey, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }

    private static async Task ExecuteScalarAsync(
        DbContext db, string sqlTemplate, long key, CancellationToken cancellationToken)
    {
        var conn = db.Database.GetDbConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = string.Format(System.Globalization.CultureInfo.InvariantCulture, sqlTemplate, key);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Stable bigint keys for Postgres session-level advisory locks. Pick once, never reuse.
/// </summary>
public static class MigrationLockKeys
{
    public const long Orchestrator = 8_000_001L;
    public const long OperationsRead = 8_000_002L;
    public const long CustomerRead = 8_000_003L;

    /// <summary>Used by <c>IngestionFailurePurgeService</c> to lease the daily purge across Worker replicas.</summary>
    public const long IngestionFailurePurge = 9_000_001L;
}
