using Microsoft.EntityFrameworkCore;
using ShippingOrchestrator.Infrastructure.Persistence;

namespace ShippingOrchestrator.PublicApi.Endpoints;

/// <summary>
/// Read-only probes the perf project hits to assert system invariants during a scenario
/// without scraping Jaeger or pg_stat_statements. Gated on <c>Performance:ProbesEnabled</c>
/// so they're never reachable in prod — the perf project flips the flag to true when
/// booting PublicApi in-process via <c>WebApplicationFactory</c>.
/// </summary>
public static class PerformanceProbeEndpoints
{
    public static void MapPerformanceProbeEndpoints(
        this IEndpointRouteBuilder app, IConfiguration configuration, IHostEnvironment env)
    {
        if (!configuration.GetValue("Performance:ProbesEnabled", false))
            return;
        // The probes read across the orchestrator + messaging schemas, which is fine for
        // in-process WebApplicationFactory boots but a clear leak in a tenant-facing host.
        // Mirror the TestTenantAuthHandler pattern: refuse to boot if someone flips the flag
        // on in production.
        if (env.IsProduction())
            throw new InvalidOperationException(
                "Performance:ProbesEnabled must be false in Production — these endpoints expose internal schema state.");

        var group = app.MapGroup("/perf").WithTags("Performance");

        // Outbox depth = the most reliable cross-pod indicator that producers are outpacing
        // consumers. OutboxLagMonitor samples the same table on a timer; scenarios prefer a
        // direct read so the assertion fires on the actual depth at decision-time.
        group.MapGet("/outbox/depth", async (OrchestratorDbContext db, CancellationToken ct) =>
        {
            var count = await db.Database
                .SqlQueryRaw<long>("SELECT COUNT(*)::bigint AS \"Value\" FROM messaging.wolverine_outgoing_envelopes")
                .SingleAsync(ct).ConfigureAwait(false);
            return Results.Ok(new { depth = count });
        }).AllowAnonymous();

        // Pending order count per tenant — proves the fire-and-forget path actually delivers
        // (publish ack ≠ commit ack; the row only appears after Worker processes the message).
        group.MapGet("/pending-orders/{tenantId:guid}/count", async (
            Guid tenantId, OrchestratorDbContext db, CancellationToken ct) =>
        {
            var count = await db.Database
                .SqlQueryRaw<long>(
                    "SELECT COUNT(*)::bigint AS \"Value\" FROM orchestrator.pending_ecommerce_orders WHERE tenant_id = {0}",
                    tenantId)
                .SingleAsync(ct).ConfigureAwait(false);
            return Results.Ok(new { tenantId, count });
        }).AllowAnonymous();
    }
}
