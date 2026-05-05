using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShippingOrchestrator.Infrastructure.Persistence;

namespace ShippingOrchestrator.Infrastructure.Telemetry;

/// <summary>
/// Periodically samples Wolverine durable outbox + inbox depth into observable gauges
/// (<c>orchestrator.outbox.depth</c>, <c>orchestrator.inbox.pending_depth</c>,
/// <c>orchestrator.inbox.oldest_scheduled_age_seconds</c>). Runs on the Worker host only —
/// the API hosts produce envelopes but the consumer-side lag is what alerts care about.
/// CloudWatch / Grafana can alarm well before WAL contention or queue starvation becomes a
/// customer-visible failure.
///
/// Why a timer + cached gauge instead of a hot gauge callback: the queries scan a busy
/// table; pulling samples into RAM and letting the metrics scrape read cached values keeps
/// the cost flat regardless of how aggressively the OTLP exporter polls.
/// </summary>
public sealed class OutboxLagMonitor : BackgroundService
{
    private static readonly TimeSpan SamplePeriod = TimeSpan.FromSeconds(30);

    private readonly IServiceProvider _services;
    private readonly ILogger<OutboxLagMonitor> _logger;

    private long _outboxDepth;
    private long _inboxPendingDepth;
    private double _inboxOldestScheduledAgeSeconds;

    public OutboxLagMonitor(IServiceProvider services, ILogger<OutboxLagMonitor> logger)
    {
        _services = services;
        _logger = logger;

        // Outgoing envelopes: every row is unsent — Wolverine deletes after successful
        // delivery — so depth is the rawest "outbox lag" signal.
        OrchestratorTelemetry.Meter.CreateObservableGauge(
            "orchestrator.outbox.depth",
            () => Volatile.Read(ref _outboxDepth),
            unit: "{envelope}",
            description: "Outstanding Wolverine outgoing envelopes (rows in messaging.wolverine_outgoing_envelopes).");

        // Incoming envelopes carry a status; rows transition to 'Handled' once the consumer
        // commits. Pending = anything still owed to a handler.
        OrchestratorTelemetry.Meter.CreateObservableGauge(
            "orchestrator.inbox.pending_depth",
            () => Volatile.Read(ref _inboxPendingDepth),
            unit: "{envelope}",
            description: "Pending Wolverine incoming envelopes (status<>'Handled').");

        OrchestratorTelemetry.Meter.CreateObservableGauge(
            "orchestrator.inbox.oldest_scheduled_age_seconds",
            () => Volatile.Read(ref _inboxOldestScheduledAgeSeconds),
            unit: "s",
            description: "Age of the oldest scheduled incoming envelope; rises during consumer outage.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // First tick fires immediately on PeriodicTimer.WaitForNextTickAsync — but only
        // AFTER waiting once, so we explicitly sample upfront so dashboards don't blank for 30s.
        using var timer = new PeriodicTimer(SamplePeriod);
        do
        {
            try
            {
                await SampleAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Swallow + log: a metrics outage must not crash the worker. Stale gauges
                // (prior values) are preferable to taking the host down.
                _logger.LogWarning(ex, "Outbox/inbox lag sample failed; reusing prior values.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task SampleAsync(CancellationToken ct)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>();

        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct).ConfigureAwait(false);

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT count(*) FROM messaging.wolverine_outgoing_envelopes";
            cmd.CommandTimeout = 5;
            var depth = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            Volatile.Write(ref _outboxDepth, Convert.ToInt64(depth, System.Globalization.CultureInfo.InvariantCulture));
        }

        await using (var cmd = connection.CreateCommand())
        {
            // FILTER only attaches to aggregate functions; the original placed it on the outer
            // extract(...), which Postgres rejects with "syntax error at or near FILTER". Apply
            // it to min(execution_time) so the aggregate is constrained to Scheduled rows;
            // when none exist min returns NULL and the extract → NULL → coalesce to 0.
            cmd.CommandText =
                "SELECT count(*) FILTER (WHERE status <> 'Handled'), " +
                "       coalesce(extract(epoch from (now() - min(execution_time) " +
                "                                    FILTER (WHERE status = 'Scheduled'))), 0) " +
                "FROM messaging.wolverine_incoming_envelopes";
            cmd.CommandTimeout = 5;
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                Volatile.Write(ref _inboxPendingDepth, reader.GetInt64(0));
                Volatile.Write(ref _inboxOldestScheduledAgeSeconds, reader.GetDouble(1));
            }
        }
    }
}
