namespace ShippingOrchestrator.Domain.Shipments;

public enum ShipmentBatchItemStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
}

public sealed class ShipmentBatchItem
{
    public Guid Id { get; private set; }
    public Guid BatchId { get; private set; }
    public Guid ShipmentId { get; private set; }
    public int OrdinalIndex { get; private set; }
    public ShipmentBatchItemStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    private ShipmentBatchItem() { }

    internal static ShipmentBatchItem Pending(Guid batchId, Guid shipmentId, int ordinalIndex) => new()
    {
        Id = Guid.NewGuid(),
        BatchId = batchId,
        ShipmentId = shipmentId,
        OrdinalIndex = ordinalIndex,
        Status = ShipmentBatchItemStatus.Pending,
    };

    internal void MarkSucceeded(DateTimeOffset now)
    {
        Status = ShipmentBatchItemStatus.Succeeded;
        ResolvedAt = now;
    }

    internal void MarkFailed(string reason, DateTimeOffset now)
    {
        Status = ShipmentBatchItemStatus.Failed;
        FailureReason = reason;
        ResolvedAt = now;
    }
}
