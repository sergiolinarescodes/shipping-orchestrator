using Microsoft.EntityFrameworkCore;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Connections;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Infrastructure.Persistence.Repositories;

internal sealed class CarrierAssignmentRepository(OrchestratorDbContext db) : ICarrierAssignmentRepository
{
    public async Task<IReadOnlyList<CarrierAssignment>> ListForTenantAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        await db.CarrierAssignments
            .Where(a => a.TenantId == tenantId && a.IsActive)
            .OrderByDescending(a => a.Priority)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(CarrierAssignment assignment, CancellationToken cancellationToken) =>
        await db.CarrierAssignments.AddAsync(assignment, cancellationToken).ConfigureAwait(false);
}
