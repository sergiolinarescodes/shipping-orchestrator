using ShippingOrchestrator.Domain.Events;

namespace ShippingOrchestrator.E2E.Tests.Wolverine;

/// <summary>
/// Wolverine handler discovered via the E2E assembly registration. Forwards every
/// <see cref="ShipmentBatchCompleted"/> domain event to the test-side
/// <see cref="BatchCompletionSignal"/> so awaiting tests can resume immediately.
/// </summary>
public static class BatchCompletionHandler
{
    public static void Handle(ShipmentBatchCompleted evt, BatchCompletionSignal signal) =>
        signal.Notify(evt);
}
