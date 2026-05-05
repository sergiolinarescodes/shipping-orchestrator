using Microsoft.EntityFrameworkCore;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Identity;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Infrastructure.Persistence.Repositories;

internal sealed class TenantMembershipRepository(OrchestratorDbContext db) : ITenantMembershipRepository
{
    public async Task AddAsync(TenantMembership membership, CancellationToken cancellationToken) =>
        await db.TenantMemberships.AddAsync(membership, cancellationToken).ConfigureAwait(false);

    public Task<TenantMembership?> FindAsync(
        AccountId accountId, TenantId tenantId, CancellationToken cancellationToken) =>
        db.TenantMemberships.FirstOrDefaultAsync(
            m => m.AccountId == accountId && m.TenantId == tenantId, cancellationToken);

    public async Task<IReadOnlyList<TenantMembership>> ListForAccountAsync(
        AccountId accountId, CancellationToken cancellationToken) =>
        await db.TenantMemberships
            .Where(m => m.AccountId == accountId)
            .OrderBy(m => m.GrantedAt)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
}
