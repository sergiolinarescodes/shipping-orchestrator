using Microsoft.EntityFrameworkCore;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Shipments;

namespace ShippingOrchestrator.Infrastructure.Persistence.Repositories;

internal sealed class ShipmentRepository(OrchestratorDbContext db) : IShipmentRepository
{
    public Task<Shipment?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Shipments
            .Include(s => s.TrackingEvents)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task AddAsync(Shipment shipment, CancellationToken cancellationToken) =>
        await db.Shipments.AddAsync(shipment, cancellationToken).ConfigureAwait(false);

    public async Task AddLineageAsync(ShipmentLineage lineage, CancellationToken cancellationToken) =>
        await db.ShipmentLineages.AddAsync(lineage, cancellationToken).ConfigureAwait(false);
}
