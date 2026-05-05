using Microsoft.EntityFrameworkCore;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Shipments;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Domain.ValueObjects;

namespace ShippingOrchestrator.Infrastructure.Persistence.Repositories;

internal sealed class ShipmentBatchRepository(OrchestratorDbContext db) : IShipmentBatchRepository
{
    public Task<ShipmentBatch?> FindAsync(Guid batchId, CancellationToken cancellationToken) =>
        db.ShipmentBatches
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

    public Task<ShipmentBatch?> FindByIdempotencyKeyAsync(
        TenantId tenantId, IdempotencyKey idempotencyKey, CancellationToken cancellationToken)
    {
        IdempotencyKey? wrapped = idempotencyKey;
        return db.ShipmentBatches
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.IdempotencyKey == wrapped, cancellationToken);
    }

    public async Task AddAsync(ShipmentBatch batch, CancellationToken cancellationToken) =>
        await db.ShipmentBatches.AddAsync(batch, cancellationToken).ConfigureAwait(false);
}
