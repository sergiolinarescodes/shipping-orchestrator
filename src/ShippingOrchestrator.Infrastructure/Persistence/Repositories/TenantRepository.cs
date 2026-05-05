using Microsoft.EntityFrameworkCore;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Infrastructure.Persistence.Repositories;

internal sealed class TenantRepository(OrchestratorDbContext db) : ITenantRepository
{
    public Task<Tenant?> FindAsync(TenantId id, CancellationToken cancellationToken) =>
        db.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task AddAsync(Tenant tenant, CancellationToken cancellationToken) =>
        await db.Tenants.AddAsync(tenant, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Tenant>> ListAsync(int take, int skip, CancellationToken cancellationToken)
    {
        var rows = await db.Tenants
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows;
    }
}
