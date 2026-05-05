namespace ShippingOrchestrator.Domain.Shipments;

/// <summary>
/// Single tracking update reported by a carrier. Owned entity on <see cref="Shipment"/>
/// (matched per-shipment by <c>EventCode</c> + <c>OccurredAt</c> for idempotency); created via
/// <see cref="Shipment.AppendTrackingEvents"/>. Stored append-only in
/// <c>orchestrator.shipment_tracking_events</c>.
/// </summary>
public sealed class ShipmentTrackingEvent
{
    public Guid Id { get; private set; }
    public Guid ShipmentId { get; private set; }
    public int Sequence { get; private set; }
    public string EventCode { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Location { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private ShipmentTrackingEvent() { }

    internal static ShipmentTrackingEvent Create(
        Guid shipmentId, int sequence, string eventCode, string? description, string? location, DateTimeOffset occurredAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipmentId,
            Sequence = sequence,
            EventCode = eventCode,
            Description = description,
            Location = location,
            OccurredAt = occurredAt,
        };
}

/// <summary>Inbound carrier-provided tracking update (no identity yet).</summary>
public sealed record ShipmentTrackingUpdate(
    string EventCode,
    string? Description,
    string? Location,
    DateTimeOffset OccurredAt);
