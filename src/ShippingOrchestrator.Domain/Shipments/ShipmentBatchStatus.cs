namespace ShippingOrchestrator.Domain.Shipments;

public enum ShipmentBatchStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    PartiallyFailed = 3,
    Failed = 4,
}
