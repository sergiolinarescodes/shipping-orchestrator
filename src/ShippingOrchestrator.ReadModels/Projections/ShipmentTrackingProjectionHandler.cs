using Microsoft.EntityFrameworkCore;
using ShippingOrchestrator.Domain.Events;
using ShippingOrchestrator.ReadModels.Customer.Persistence;
using ShippingOrchestrator.ReadModels.Operations.Persistence;
using ShippingOrchestrator.ReadModels.Realtime;

namespace ShippingOrchestrator.ReadModels.Projections;

/// <summary>
/// Fans <see cref="ShipmentTrackingUpdated"/> into both schemas. Each individual carrier event
/// becomes a row keyed by (shipmentId, sequence) so the dashboard can render an ordered
/// timeline. Idempotent — Wolverine retries that re-publish the same domain event are caught
/// by the unique index.
/// </summary>
public static class ShipmentTrackingProjectionHandler
{
    public static async Task Handle(
        ShipmentTrackingUpdated @event,
        OperationsReadDbContext ops,
        CustomerReadDbContext customer,
        IDashboardBroadcaster broadcaster,
        CancellationToken ct)
    {
        // Project (code, occurred-at) tuples instead of full entities so the dedup is O(1) per
        // incoming update and we don't drag entity tracking into the membership check.
        var existingOps = await ops.ShipmentTrackingEvents
            .Where(e => e.ShipmentId == @event.ShipmentId)
            .Select(e => new { e.EventCode, e.OccurredAt, e.Sequence })
            .ToListAsync(ct).ConfigureAwait(false);
        var opsKeys = existingOps.Select(e => (e.EventCode, e.OccurredAt)).ToHashSet();
        var nextOpsSeq = existingOps.Count == 0 ? 0 : existingOps.Max(e => e.Sequence) + 1;
        foreach (var update in @event.Events)
        {
            if (!opsKeys.Add((update.EventCode, update.OccurredAt))) continue;
            ops.ShipmentTrackingEvents.Add(new OpsShipmentTrackingEventEntity
            {
                Id = Guid.NewGuid(),
                ShipmentId = @event.ShipmentId,
                Sequence = nextOpsSeq++,
                EventCode = update.EventCode,
                Description = update.Description,
                Location = update.Location,
                OccurredAt = update.OccurredAt,
            });
        }

        var existingCustomer = await customer.ShipmentTrackingEvents
            .Where(e => e.ShipmentId == @event.ShipmentId)
            .Select(e => new { e.EventCode, e.OccurredAt, e.Sequence })
            .ToListAsync(ct).ConfigureAwait(false);
        var customerKeys = existingCustomer.Select(e => (e.EventCode, e.OccurredAt)).ToHashSet();
        var nextCustSeq = existingCustomer.Count == 0 ? 0 : existingCustomer.Max(e => e.Sequence) + 1;
        foreach (var update in @event.Events)
        {
            if (!customerKeys.Add((update.EventCode, update.OccurredAt))) continue;
            customer.ShipmentTrackingEvents.Add(new CustomerShipmentTrackingEventEntity
            {
                Id = Guid.NewGuid(),
                ShipmentId = @event.ShipmentId,
                TenantId = @event.TenantId.Value,
                Sequence = nextCustSeq++,
                EventCode = update.EventCode,
                Description = update.Description,
                Location = update.Location,
                OccurredAt = update.OccurredAt,
            });
        }

        if (!string.IsNullOrWhiteSpace(@event.CurrentStatus))
        {
            var promoteTo = @event.CurrentStatus switch
            {
                "Delivered" => "Delivered",
                "InTransit" or "Accepted" => "InTransit",
                _ => null,
            };
            if (promoteTo is not null)
            {
                var opsShipment = await ops.Shipments.FindAsync([@event.ShipmentId], ct).ConfigureAwait(false);
                if (opsShipment is not null && opsShipment.Status != "Failed" && opsShipment.Status != "Cancelled")
                {
                    opsShipment.Status = promoteTo;
                    opsShipment.UpdatedAt = @event.OccurredAt;
                }
                var customerShipment = await customer.Shipments.FindAsync([@event.ShipmentId], ct).ConfigureAwait(false);
                if (customerShipment is not null && customerShipment.Status != "Failed" && customerShipment.Status != "Cancelled")
                {
                    customerShipment.Status = promoteTo;
                    customerShipment.UpdatedAt = @event.OccurredAt;
                }
            }
        }

        await ProjectionSaveExtensions.SaveAndBroadcastAsync(
            ops, customer, broadcaster, @event.TenantId.Value, DashboardArea.Shipments, ct);
    }
}
