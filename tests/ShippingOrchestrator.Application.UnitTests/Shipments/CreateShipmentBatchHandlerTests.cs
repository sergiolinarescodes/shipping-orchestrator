using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Application.Shipments;
using ShippingOrchestrator.Domain.Shipments;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Domain.ValueObjects;

namespace ShippingOrchestrator.Application.UnitTests.Shipments;

[TestFixture]
public class CreateShipmentBatchHandlerTests
{
    private static IClock NewClock(DateTimeOffset at)
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(at);
        return c;
    }

    private static ShipmentRequestDto NewRequest(string fromCountry, string toCountry) => new(
        From: new Address("Acme", "Line 1", null, "Amsterdam", null, "1012 AB", new CountryCode(fromCountry)),
        To: new Address("Customer", "Line 1", null, "Brussels", null, "1000", new CountryCode(toCountry)),
        Parcel: new Parcel(Weight.FromGrams(500), new Dimension(100, 100, 100), new Money(10m, "EUR")),
        PreferredServiceCode: "STANDARD");

    [Test]
    public async Task Each_shipment_is_created_with_the_pre_allocated_batch_id()
    {
        var tenant = TenantId.New();
        var now = DateTimeOffset.UtcNow;
        var clock = NewClock(now);
        var shipments = Substitute.For<IShipmentRepository>();
        var batches = Substitute.For<IShipmentBatchRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var captured = new List<Shipment>();
        shipments.AddAsync(Arg.Do<Shipment>(s => captured.Add(s)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var (result, processCommand) = await CreateShipmentBatchHandler.Handle(
            new CreateShipmentBatchCommand(tenant, IdempotencyKey: null,
                Shipments: [NewRequest("NL", "BE"), NewRequest("NL", "DE")]),
            shipments, batches, uow, clock, CancellationToken.None);

        captured.Should().HaveCount(2);
        captured.Select(s => s.BatchId).Should().AllBeEquivalentTo(result.BatchId,
            "shipments must be created already linked to their batch so the customer-read projection " +
            "indexes them under the right BatchId from the very first ShipmentCreated event");
        processCommand.Should().NotBeNull();
        processCommand.BatchId.Should().Be(result.BatchId);
    }

    [Test]
    public async Task Empty_shipment_list_throws_argument_exception()
    {
        var act = () => CreateShipmentBatchHandler.Handle(
            new CreateShipmentBatchCommand(TenantId.New(), null, []),
            Substitute.For<IShipmentRepository>(),
            Substitute.For<IShipmentBatchRepository>(),
            Substitute.For<IUnitOfWork>(),
            NewClock(DateTimeOffset.UtcNow),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task Idempotency_key_returns_existing_batch_without_re_publishing_process_command()
    {
        var tenant = TenantId.New();
        var clock = NewClock(DateTimeOffset.UtcNow);
        var key = IdempotencyKey.Parse("abcd-efgh-1234-5678");
        var existing = ShipmentBatch.Accept(tenant, key, [Guid.NewGuid()], clock.UtcNow);

        var batches = Substitute.For<IShipmentBatchRepository>();
        batches.FindByIdempotencyKeyAsync(tenant, key, Arg.Any<CancellationToken>())
            .Returns(existing);

        var (result, processCommand) = await CreateShipmentBatchHandler.Handle(
            new CreateShipmentBatchCommand(tenant, key.Value, [NewRequest("NL", "BE")]),
            Substitute.For<IShipmentRepository>(),
            batches,
            Substitute.For<IUnitOfWork>(),
            clock,
            CancellationToken.None);

        result.BatchId.Should().Be(existing.Id);
        processCommand.Should().BeNull("the existing batch already cascaded its ProcessShipmentBatchCommand on first call");
    }
}
