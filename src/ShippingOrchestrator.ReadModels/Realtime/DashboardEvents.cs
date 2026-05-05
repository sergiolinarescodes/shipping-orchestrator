namespace ShippingOrchestrator.ReadModels.Realtime;

public static class DashboardEvents
{
    public const string Invalidate = "dashboard:invalidate";
}

public static class DashboardArea
{
    public const string Orders = "orders";
    public const string Shipments = "shipments";
    public const string Batches = "batches";
    public const string NeedsAttention = "needs-attention";
}
