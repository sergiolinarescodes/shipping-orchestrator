namespace ShippingOrchestrator.Domain.Shipments;

/// <summary>
/// Append-only audit row written every time a shipment changes status. Mirrors the
/// EventLineage pattern from the reference repo so support staff can answer
/// "why did this shipment go to carrier X?" without replaying domain events.
/// Stored alongside the shipment in the orchestrator schema; never edited.
/// </summary>
public sealed class ShipmentLineage
{
    public long Id { get; }
    public Guid ShipmentId { get; private set; }
    public ShipmentStatus FromStatus { get; private set; }
    public ShipmentStatus ToStatus { get; private set; }
    public string Actor { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public string? RuleAttribution { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private ShipmentLineage() { }

    public static ShipmentLineage Record(
        Guid shipmentId,
        ShipmentStatus from,
        ShipmentStatus to,
        string actor,
        DateTimeOffset occurredAt,
        string? reason = null,
        string? ruleAttribution = null) => new()
        {
            ShipmentId = shipmentId,
            FromStatus = from,
            ToStatus = to,
            Actor = actor,
            OccurredAt = occurredAt,
            Reason = reason,
            RuleAttribution = ruleAttribution,
        };
}
