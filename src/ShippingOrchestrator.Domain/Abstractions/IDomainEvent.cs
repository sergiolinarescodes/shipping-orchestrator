namespace ShippingOrchestrator.Domain.Abstractions;

/// <summary>
/// Marker for facts the domain produces that the rest of the system reacts to.
/// Events are immutable, dispatched by the outbox after the producing transaction commits,
/// and consumed by Wolverine handlers in the Worker host (Read Platform projections, sagas).
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
