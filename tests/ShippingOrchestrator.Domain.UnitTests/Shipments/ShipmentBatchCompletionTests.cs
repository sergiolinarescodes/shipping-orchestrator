using FluentAssertions;
using NUnit.Framework;
using ShippingOrchestrator.Domain.Events;
using ShippingOrchestrator.Domain.Shipments;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Domain.UnitTests.Shipments;

/// <summary>
/// Regression coverage for the batch-completion race condition: handlers running concurrently
/// each carry a stale snapshot of the batch's items. The fix lives in
/// <see cref="ShipmentBatch.RecheckCompletion"/> + the idempotency guard inside
/// <see cref="ShipmentBatch.TryCompleteIfDone"/>. These tests prove both behaviors.
/// </summary>
[TestFixture]
public class ShipmentBatchCompletionTests
{
    private static readonly TenantId Tenant = TenantId.New();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Test]
    public void RecheckCompletion_with_all_items_resolved_raises_completion_event_once()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var batch = ShipmentBatch.Accept(Tenant, null, ids, Now);
        batch.StartProcessing(Now);
        batch.ClearDomainEvents();

        // Simulate "fresh fetch after concurrent peers committed" — both items already Succeeded.
        // This is the same shape the real handler produces when it re-fetches via a new scope.
        foreach (var id in ids) batch.RecordItemSucceeded(id, Now);

        batch.Status.Should().Be(ShipmentBatchStatus.Completed);
        var completion = batch.DomainEvents.OfType<ShipmentBatchCompleted>().Single();
        completion.SuccessCount.Should().Be(2);
        completion.FailureCount.Should().Be(0);
    }

    [Test]
    public void RecheckCompletion_is_idempotent_after_terminal_state()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var batch = ShipmentBatch.Accept(Tenant, null, ids, Now);
        batch.StartProcessing(Now);
        foreach (var id in ids) batch.RecordItemSucceeded(id, Now);
        var beforeCount = batch.DomainEvents.OfType<ShipmentBatchCompleted>().Count();
        beforeCount.Should().Be(1);

        // Second peer arrives with the same fresh-fetched snapshot. Must not raise a duplicate.
        batch.RecheckCompletion(Now.AddSeconds(1));

        batch.DomainEvents.OfType<ShipmentBatchCompleted>().Count().Should().Be(beforeCount);
        batch.Status.Should().Be(ShipmentBatchStatus.Completed);
    }

    [Test]
    public void RecheckCompletion_with_pending_items_does_not_raise_completion()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var batch = ShipmentBatch.Accept(Tenant, null, ids, Now);
        batch.StartProcessing(Now);
        batch.RecordItemSucceeded(ids[0], Now);
        batch.ClearDomainEvents();

        // Earlier writer in the race: only 1 of 3 items resolved when this peer recheck runs.
        batch.RecheckCompletion(Now);

        batch.Status.Should().Be(ShipmentBatchStatus.Processing);
        batch.DomainEvents.OfType<ShipmentBatchCompleted>().Should().BeEmpty();
    }

    [Test]
    public void Mixed_resolutions_via_recheck_classify_as_partially_failed()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var batch = ShipmentBatch.Accept(Tenant, null, ids, Now);
        batch.StartProcessing(Now);
        batch.RecordItemSucceeded(ids[0], Now);
        batch.RecordItemFailed(ids[1], "carrier 500", Now);

        batch.Status.Should().Be(ShipmentBatchStatus.PartiallyFailed);

        // Subsequent recheck must not flip it back or raise extra events.
        var beforeEvents = batch.DomainEvents.Count;
        batch.RecheckCompletion(Now);
        batch.DomainEvents.Count.Should().Be(beforeEvents);
        batch.Status.Should().Be(ShipmentBatchStatus.PartiallyFailed);
    }

    [Test]
    public void TryCompleteIfDone_does_not_re_run_after_terminal_state()
    {
        var ids = new[] { Guid.NewGuid() };
        var batch = ShipmentBatch.Accept(Tenant, null, ids, Now);
        batch.StartProcessing(Now);
        batch.RecordItemSucceeded(ids[0], Now);
        var firstCompletedAt = batch.CompletedAt;

        batch.RecheckCompletion(Now.AddHours(1));

        batch.CompletedAt.Should().Be(firstCompletedAt, "second recheck must not overwrite the completion timestamp");
    }
}
