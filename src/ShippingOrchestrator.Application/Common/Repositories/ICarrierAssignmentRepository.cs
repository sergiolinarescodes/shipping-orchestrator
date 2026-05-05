using ShippingOrchestrator.Domain.Connections;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Application.Common.Repositories;

public interface ICarrierAssignmentRepository
{
    Task<IReadOnlyList<CarrierAssignment>> ListForTenantAsync(TenantId tenantId, CancellationToken cancellationToken);
    Task AddAsync(CarrierAssignment assignment, CancellationToken cancellationToken);
}
