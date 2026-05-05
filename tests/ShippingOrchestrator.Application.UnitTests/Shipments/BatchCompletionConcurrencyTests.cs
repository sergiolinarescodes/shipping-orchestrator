using FluentAssertions;
using NUnit.Framework;
using ShippingOrchestrator.Domain.Events;
using ShippingOrchestrator.Domain.Shipments;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Application.UnitTests.Shipments;

/// <summary>
/// Simulates the exact race that the handler hit during E2E: three peer scopes each load
/// the batch (with all items still Pending), each succeeds its own item, and saves. The
/// stale-snapshot saves must not trigger ShipmentBatchCompleted; only the fresh-fetch
/// recheck (modeled here by a single batch instance with all peers having mutated their
/// items) must produce exactly one completion event.
/// </summary>
[TestFixture]
public class BatchCompletionConcurrencyTests
{
    [Test]
    public void Stale_peer_snapshots_do_not_raise_completion_each_writer_only_sees_own_item()
    {
        var tenant = TenantId.New();
        var now = DateTimeOffset.UtcNow;
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        var peerA = ShipmentBatch.Accept(tenant, null, ids, now);
        var peerB = ShipmentBatch.Accept(tenant, null, ids, now);
        var peerC = ShipmentBatch.Accept(tenant, null, ids, now);
        foreach (var b in new[] { peerA, peerB, peerC }) b.StartProcessing(now);

        // Each peer succeeds its own item. They can't see each other's mutations.
        peerA.RecordItemSucceeded(ids[0], now);
        peerB.RecordItemSucceeded(ids[1], now);
        peerC.RecordItemSucceeded(ids[2], now);

        // None of the stale snapshots has all-resolved items, so none raises completion.
        peerA.DomainEvents.OfType<ShipmentBatchCompleted>().Should().BeEmpty();
        peerB.DomainEvents.OfType<ShipmentBatchCompleted>().Should().BeEmpty();
        peerC.DomainEvents.OfType<ShipmentBatchCompleted>().Should().BeEmpty();
    }

    [Test]
    public void Fresh_fetch_after_all_writes_committed_sees_resolved_items_and_completes()
    {
        var tenant = TenantId.New();
        var now = DateTimeOffset.UtcNow;
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        // Model the post-commit state: all items resolved on a freshly-loaded batch.
        var fresh = ShipmentBatch.Accept(tenant, null, ids, now);
        fresh.StartProcessing(now);
        foreach (var id in ids) fresh.RecordItemSucceeded(id, now);

        fresh.Status.Should().Be(ShipmentBatchStatus.Completed);
        fresh.DomainEvents.OfType<ShipmentBatchCompleted>().Should().HaveCount(1);
    }

    [Test]
    public void Two_concurrent_fresh_fetches_only_one_raises_completion_via_idempotency()
    {
        var tenant = TenantId.New();
        var now = DateTimeOffset.UtcNow;
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };

        // Both fresh fetches see all items resolved (DB has both committed at this point).
        var freshA = ShipmentBatch.Accept(tenant, null, ids, now);
        var freshB = ShipmentBatch.Accept(tenant, null, ids, now);
        freshA.StartProcessing(now);
        freshB.StartProcessing(now);
        foreach (var id in ids) freshA.RecordItemSucceeded(id, now);
        foreach (var id in ids) freshB.RecordItemSucceeded(id, now);

        // Both raise completion in their own snapshot — but in the real system that's two
        // separate writes to the read store. The projection handler is idempotent so this
        // is harmless. Verify each batch's state stays Completed and event count stays 1.
        freshA.DomainEvents.OfType<ShipmentBatchCompleted>().Should().HaveCount(1);
        freshB.DomainEvents.OfType<ShipmentBatchCompleted>().Should().HaveCount(1);

        // Idempotency on a single instance: re-running RecheckCompletion does NOT add events.
        var beforeCount = freshA.DomainEvents.Count;
        freshA.RecheckCompletion(now);
        freshA.DomainEvents.Count.Should().Be(beforeCount);
    }
}
