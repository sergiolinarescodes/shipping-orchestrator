using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Domain.Abstractions;
using Wolverine;

namespace ShippingOrchestrator.Infrastructure.Persistence;

/// <summary>
/// Saves the orchestrator <see cref="OrchestratorDbContext"/> and then publishes every
/// domain event raised on the tracked aggregates so the Read Platform's projection
/// handlers (and other subscribers) react. Sufficient for first-cut in-memory transport;
/// production swaps in Wolverine's transactional outbox so events ship only after commit.
/// </summary>
internal sealed class TransactionalUnitOfWork(OrchestratorDbContext db, IMessageBus bus) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        var aggregates = db.ChangeTracker.Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var pending = aggregates.SelectMany(a => a.DomainEvents).ToList();

        var written = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var @event in pending)
            await bus.PublishAsync(@event).ConfigureAwait(false);

        foreach (var aggregate in aggregates)
            aggregate.ClearDomainEvents();

        return written;
    }
}
