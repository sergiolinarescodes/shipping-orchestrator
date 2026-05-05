namespace ShippingOrchestrator.ReadModels.Realtime;

/// <summary>
/// Port that lets Worker-resident projection handlers push tenant-scoped invalidation events
/// across the process boundary to PublicApi pods (where SignalR clients are connected).
/// Lives in ReadModels because layer rules forbid Application/Infrastructure refs from this
/// assembly. Implementation is wired in Infrastructure via Wolverine; the projection handler
/// receives the abstraction through DI without knowing the transport.
/// </summary>
public interface IDashboardBroadcaster
{
    /// <summary>
    /// Best-effort fan-out. The broadcast enrolls in Wolverine's durable outbox so a Worker
    /// crash between the projection commit and outbox flush merely delays delivery; the next
    /// projection retry replays both. SPA polling cadence catches any missed event.
    /// </summary>
    /// <param name="tenantId">Customer tenant id; the SignalR group key.</param>
    /// <param name="eventName">Hub method name (e.g. <c>"dashboard:invalidate"</c>).</param>
    /// <param name="area">Optional logical area hint (<c>"orders"</c>, <c>"shipments"</c>,
    /// <c>"batches"</c>, <c>"needs-attention"</c>) so the SPA can scope its invalidations.</param>
    Task BroadcastAsync(
        Guid tenantId,
        string eventName,
        string? area,
        CancellationToken cancellationToken);
}
