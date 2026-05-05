using ShippingOrchestrator.Domain.Ingestion;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Application.Common.Repositories;

/// <summary>
/// Inbox-style repository for normalized ecommerce orders that haven't been bundled into a
/// shipment batch yet. The customer dashboard reads from <see cref="ListPendingForTenantAsync"/>
/// and the bundle command consumes via <see cref="LoadManyAsync"/> + <see cref="MarkConsumedAsync"/>.
/// </summary>
public interface IPendingEcommerceOrderRepository
{
    Task<PendingEcommerceOrder?> FindByExternalIdAsync(
        TenantId tenantId,
        string platformCode,
        string externalOrderId,
        CancellationToken cancellationToken);

    Task AddAsync(PendingEcommerceOrder order, CancellationToken cancellationToken);

    Task<IReadOnlyList<PendingEcommerceOrder>> ListPendingForTenantAsync(
        TenantId tenantId,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PendingEcommerceOrder>> LoadManyAsync(
        TenantId tenantId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);

    Task MarkConsumedAsync(
        IReadOnlyCollection<Guid> ids,
        Guid batchId,
        DateTimeOffset consumedAt,
        CancellationToken cancellationToken);
}
