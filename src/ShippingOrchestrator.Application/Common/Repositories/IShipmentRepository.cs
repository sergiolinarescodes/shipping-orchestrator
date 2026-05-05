using ShippingOrchestrator.Domain.Shipments;

namespace ShippingOrchestrator.Application.Common.Repositories;

public interface IShipmentRepository
{
    Task<Shipment?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Shipment shipment, CancellationToken cancellationToken);
    Task AddLineageAsync(ShipmentLineage lineage, CancellationToken cancellationToken);
}
