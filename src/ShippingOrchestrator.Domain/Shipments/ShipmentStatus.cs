namespace ShippingOrchestrator.Domain.Shipments;

public enum ShipmentStatus
{
    Created = 0,
    CarrierSelected = 1,
    LabelRequested = 2,
    Labeled = 3,
    InTransit = 4,
    Delivered = 5,
    Cancelled = 6,
    Failed = 7,
}
