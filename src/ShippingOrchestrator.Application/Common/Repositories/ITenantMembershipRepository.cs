using ShippingOrchestrator.Domain.Identity;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Application.Common.Repositories;

public interface ITenantMembershipRepository
{
    Task AddAsync(TenantMembership membership, CancellationToken cancellationToken);

    Task<TenantMembership?> FindAsync(
        AccountId accountId,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TenantMembership>> ListForAccountAsync(
        AccountId accountId,
        CancellationToken cancellationToken);
}
