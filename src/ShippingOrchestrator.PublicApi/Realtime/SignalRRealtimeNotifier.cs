using Microsoft.AspNetCore.SignalR;

namespace ShippingOrchestrator.PublicApi.Realtime;

internal sealed class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<RealtimeHub> _hub;

    public SignalRRealtimeNotifier(IHubContext<RealtimeHub> hub)
    {
        _hub = hub;
    }

    public Task NotifyDashboardAsync(
        Guid tenantId, string eventName, object? payload = null, CancellationToken cancellationToken = default)
        => _hub.Clients.Group(RealtimeHub.GroupName(tenantId))
            .SendAsync(eventName, payload ?? new { }, cancellationToken);
}
