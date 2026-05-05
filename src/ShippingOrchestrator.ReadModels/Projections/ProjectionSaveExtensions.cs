using Microsoft.EntityFrameworkCore;
using ShippingOrchestrator.ReadModels.Customer.Persistence;
using ShippingOrchestrator.ReadModels.Operations.Persistence;
using ShippingOrchestrator.ReadModels.Realtime;

namespace ShippingOrchestrator.ReadModels.Projections;

internal static class ProjectionSaveExtensions
{
    /// <summary>
    /// Sequential, ops-first persistence + best-effort dashboard fan-out shared by every
    /// projection handler. Wolverine retries the whole handler on failure, and each upsert is
    /// keyed by primary key so a redelivery converges; parallel saves can leave the two schemas
    /// drifted between a partial failure and the next replay. The broadcast is suppressed when
    /// neither context mutated, so a no-op replay (e.g. <c>Resolved</c> redelivered after the
    /// row is already <c>Resolved</c>) doesn't fan a redundant invalidation across SignalR.
    /// </summary>
    public static async Task SaveAndBroadcastAsync(
        OperationsReadDbContext ops,
        CustomerReadDbContext customer,
        IDashboardBroadcaster broadcaster,
        Guid tenantId,
        string area,
        CancellationToken ct)
    {
        var changed = ops.ChangeTracker.HasChanges() || customer.ChangeTracker.HasChanges();
        await ops.SaveChangesAsync(ct).ConfigureAwait(false);
        await customer.SaveChangesAsync(ct).ConfigureAwait(false);
        if (changed)
            await broadcaster.BroadcastAsync(tenantId, DashboardEvents.Invalidate, area, ct).ConfigureAwait(false);
    }
}
