using ShippingOrchestrator.Application.Realtime;

namespace ShippingOrchestrator.PublicApi.Realtime;

/// <summary>
/// PublicApi-only handler that turns a Worker-published <see cref="BroadcastDashboardEvent"/>
/// into a SignalR hub fan-out. Wolverine discovers this handler because PublicApi adds its
/// own assembly to the configuration in <c>Program.cs</c>.
/// </summary>
public static class BroadcastDashboardHandler
{
    public static Task Handle(
        BroadcastDashboardEvent message,
        IRealtimeNotifier notifier,
        CancellationToken cancellationToken)
        => notifier.NotifyDashboardAsync(
            message.TenantId,
            message.EventName,
            new { area = message.Area },
            cancellationToken);
}
