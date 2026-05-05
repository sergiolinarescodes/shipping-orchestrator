using ShippingOrchestrator.Application.Realtime;
using ShippingOrchestrator.ReadModels.Realtime;
using Wolverine;

namespace ShippingOrchestrator.Worker.Realtime;

/// <summary>
/// Worker-resident <see cref="IDashboardBroadcaster"/>. Projection handlers running in this
/// host call it after <c>customer.SaveChangesAsync</c>; it publishes a Wolverine
/// <see cref="BroadcastDashboardEvent"/> command which conventional routing fans out to the
/// SQS queue PublicApi listens on. Lives here (not in Infrastructure) so the layer-rule
/// invariant — Infrastructure must not reference ReadModels — stays intact.
/// </summary>
internal sealed class WolverineDashboardBroadcaster : IDashboardBroadcaster
{
    private readonly IMessageBus _bus;

    public WolverineDashboardBroadcaster(IMessageBus bus)
    {
        _bus = bus;
    }

    public Task BroadcastAsync(Guid tenantId, string eventName, string? area, CancellationToken cancellationToken)
        => _bus.SendAsync(new BroadcastDashboardEvent(tenantId, eventName, area)).AsTask();
}
