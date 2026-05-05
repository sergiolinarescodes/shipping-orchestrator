using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Application.Common.Repositories;

public interface ITenantRepository
{
    Task<Tenant?> FindAsync(TenantId id, CancellationToken cancellationToken);
    Task AddAsync(Tenant tenant, CancellationToken cancellationToken);
    Task<IReadOnlyList<Tenant>> ListAsync(int take, int skip, CancellationToken cancellationToken);
}
