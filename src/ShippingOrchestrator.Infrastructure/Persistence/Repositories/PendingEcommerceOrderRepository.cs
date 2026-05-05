using Microsoft.EntityFrameworkCore;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Ingestion;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Infrastructure.Persistence.Repositories;

internal sealed class PendingEcommerceOrderRepository(OrchestratorDbContext db) : IPendingEcommerceOrderRepository
{
    public Task<PendingEcommerceOrder?> FindByExternalIdAsync(
        TenantId tenantId, string platformCode, string externalOrderId, CancellationToken cancellationToken) =>
        db.PendingEcommerceOrders.FirstOrDefaultAsync(
            p => p.TenantId == tenantId
                 && p.PlatformCode == platformCode
                 && p.ExternalOrderId == externalOrderId,
            cancellationToken);

    public async Task AddAsync(PendingEcommerceOrder order, CancellationToken cancellationToken) =>
        await db.PendingEcommerceOrders.AddAsync(order, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<PendingEcommerceOrder>> ListPendingForTenantAsync(
        TenantId tenantId, int take, CancellationToken cancellationToken) =>
        await db.PendingEcommerceOrders
            .Where(p => p.TenantId == tenantId && p.ConsumedAt == null)
            .OrderBy(p => p.IngestedAt)
            .Take(take)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<PendingEcommerceOrder>> LoadManyAsync(
        TenantId tenantId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
            return Array.Empty<PendingEcommerceOrder>();
        return await db.PendingEcommerceOrders
            .Where(p => p.TenantId == tenantId && ids.Contains(p.Id))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task MarkConsumedAsync(
        IReadOnlyCollection<Guid> ids,
        Guid batchId,
        DateTimeOffset consumedAt,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
            return;
        var rows = await db.PendingEcommerceOrders
            .Where(p => ids.Contains(p.Id))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var row in rows)
        {
            if (!row.IsConsumed)
                row.MarkConsumed(batchId, consumedAt);
        }
    }
}
