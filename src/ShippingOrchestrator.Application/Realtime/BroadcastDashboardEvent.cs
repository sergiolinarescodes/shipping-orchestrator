namespace ShippingOrchestrator.Application.Realtime;

/// <summary>
/// Wolverine message contract for the cross-process dashboard push hop. Worker (projection
/// handlers) sends; PublicApi (the only host that knows about SignalR) handles. Conventional
/// routing creates the SQS queue automatically — Worker has the message type via Application
/// discovery but no handler, PublicApi has both via its own assembly being added to the
/// discovery list in <c>Program.cs</c>.
/// </summary>
public sealed record BroadcastDashboardEvent(Guid TenantId, string EventName, string? Area);
