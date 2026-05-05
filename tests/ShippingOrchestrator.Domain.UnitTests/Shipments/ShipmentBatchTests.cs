using FluentAssertions;
using NUnit.Framework;
using ShippingOrchestrator.Domain.Shipments;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Domain.UnitTests.Shipments;

[TestFixture]
public class ShipmentBatchTests
{
    private static readonly TenantId Tenant = TenantId.New();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Test]
    public void Accept_creates_batch_in_pending_with_items()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var batch = ShipmentBatch.Accept(Tenant, idempotencyKey: null, ids, Now);

        batch.Status.Should().Be(ShipmentBatchStatus.Pending);
        batch.Items.Should().HaveCount(2);
        batch.Items.Select(i => i.ShipmentId).Should().BeEquivalentTo(ids);
    }

    [Test]
    public void All_items_succeed_completes_batch_with_completed_status()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var batch = ShipmentBatch.Accept(Tenant, null, ids, Now);
        batch.StartProcessing(Now);

        foreach (var id in ids) batch.RecordItemSucceeded(id, Now);

        batch.Status.Should().Be(ShipmentBatchStatus.Completed);
        batch.CompletedAt.Should().NotBeNull();
    }

    [Test]
    public void Mixed_outcomes_yield_partially_failed()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var batch = ShipmentBatch.Accept(Tenant, null, ids, Now);
        batch.StartProcessing(Now);

        batch.RecordItemSucceeded(ids[0], Now);
        batch.RecordItemFailed(ids[1], "carrier 500", Now);

        batch.Status.Should().Be(ShipmentBatchStatus.PartiallyFailed);
    }

    [Test]
    public void All_items_fail_yields_failed()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var batch = ShipmentBatch.Accept(Tenant, null, ids, Now);
        batch.StartProcessing(Now);

        foreach (var id in ids) batch.RecordItemFailed(id, "down", Now);

        batch.Status.Should().Be(ShipmentBatchStatus.Failed);
    }

    [Test]
    public void Empty_batch_throws()
    {
        var act = () => ShipmentBatch.Accept(Tenant, null, [], Now);
        act.Should().Throw<ArgumentException>();
    }
}
