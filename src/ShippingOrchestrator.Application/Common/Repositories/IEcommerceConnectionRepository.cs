using ShippingOrchestrator.Domain.Connections;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Application.Common.Repositories;

public interface IEcommerceConnectionRepository
{
    Task<EcommerceConnection?> FindAsync(Guid connectionId, CancellationToken cancellationToken);
    Task<EcommerceConnection?> FindByExternalAccountAsync(
        TenantId tenantId,
        string platformCode,
        string externalAccountId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reverse lookup used by anonymous webhook endpoints — a Shopify push only carries the
    /// shop domain, not the tenant id, so the platform-scoped (platform, externalAccountId)
    /// pair must be unique across tenants. Enforced by the existing unique index.
    /// </summary>
    Task<EcommerceConnection?> FindByPlatformAccountAsync(
        string platformCode,
        string externalAccountId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EcommerceConnection>> ListForTenantAsync(TenantId tenantId, CancellationToken cancellationToken);

    Task AddAsync(EcommerceConnection connection, CancellationToken cancellationToken);

    /// <summary>
    /// Hard-removes the connection row. Disconnect is a destructive action in this product:
    /// the tenant flow is "remove and re-install fresh" rather than "deactivate then re-activate"
    /// so per-tenant credentials, webhook secrets, and platform-side hooks are always
    /// regenerated on each Connect — no stale state can survive a disconnect.
    /// </summary>
    void Remove(EcommerceConnection connection);
}
