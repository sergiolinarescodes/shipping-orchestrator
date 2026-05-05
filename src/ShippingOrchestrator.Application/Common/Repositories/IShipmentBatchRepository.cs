using ShippingOrchestrator.Domain.Shipments;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Domain.ValueObjects;

namespace ShippingOrchestrator.Application.Common.Repositories;

public interface IShipmentBatchRepository
{
    Task<ShipmentBatch?> FindAsync(Guid batchId, CancellationToken cancellationToken);
    Task<ShipmentBatch?> FindByIdempotencyKeyAsync(
        TenantId tenantId,
        IdempotencyKey idempotencyKey,
        CancellationToken cancellationToken);
    Task AddAsync(ShipmentBatch batch, CancellationToken cancellationToken);
}
