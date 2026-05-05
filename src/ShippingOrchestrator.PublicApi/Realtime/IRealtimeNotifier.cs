namespace ShippingOrchestrator.PublicApi.Realtime;

/// <summary>
/// Pushes a tenant-scoped event to any SignalR client subscribed to the tenant's group.
/// Implementations must be safe for concurrent calls from any number of HTTP request scopes.
/// </summary>
public interface IRealtimeNotifier
{
    Task NotifyDashboardAsync(
        Guid tenantId, string eventName, object? payload = null, CancellationToken cancellationToken = default);
}
