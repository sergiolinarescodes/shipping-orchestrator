using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using NUnit.Framework;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Application.Ingestion;
using ShippingOrchestrator.Domain.Ingestion;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Domain.ValueObjects;
using ShippingOrchestrator.Infrastructure.Wolverine;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;
using Wolverine;

namespace ShippingOrchestrator.Infrastructure.IntegrationTests;

/// <summary>
/// Pure unit tests — no Postgres or Wolverine runtime. The dispatcher's contract: pre-check
/// the unique index, return AlreadyPending synchronously when the row already exists,
/// otherwise pre-allocate a pending id and publish fire-and-forget. The hash function must
/// be stable so the same tenant always lands in the same shard across processes.
/// </summary>
[TestFixture]
public class IngestionDispatcherTests
{
    [Test]
    public void ResolveShard_IsDeterministic()
    {
        var tenant = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var first = IngestionDispatcher.ResolveShard(tenant, 8);
        var second = IngestionDispatcher.ResolveShard(tenant, 8);

        first.Should().Be(second);
        first.Should().BeInRange(0, 7);
    }

    [Test]
    public void ResolveShard_DistributesAcrossSlots()
    {
        const int shardCount = 8;
        var counts = new int[shardCount];
        for (var i = 0; i < 4_000; i++)
            counts[IngestionDispatcher.ResolveShard(Guid.NewGuid(), shardCount)] += 1;

        counts.Should().AllSatisfy(c => c.Should().BeGreaterThan(50),
            "FNV-1a over random Guid bytes should populate every shard slot.");
    }

    [Test]
    public async Task DispatchAsync_ReturnsAlreadyPending_WhenRowExists()
    {
        var tenantId = TenantId.New();
        var payload = Payload(tenantId, "ORD-DUP");
        var existing = new PendingEcommerceOrder(
            Guid.NewGuid(), tenantId, "shopify", "ORD-DUP", "{}", DateTimeOffset.UtcNow);

        var bus = Substitute.For<IMessageBus>();
        var pendingRepo = Substitute.For<IPendingEcommerceOrderRepository>();
        pendingRepo.FindByExternalIdAsync(tenantId, "shopify", "ORD-DUP", Arg.Any<CancellationToken>())
            .Returns(existing);
        var dispatcher = new IngestionDispatcher(bus, pendingRepo, BuildConfig(shardCount: null));

        var ack = await dispatcher.DispatchAsync(payload, CancellationToken.None);

        ack.AlreadyPending.Should().BeTrue();
        ack.PendingOrderId.Should().Be(existing.Id);
        await bus.DidNotReceive().SendAsync(Arg.Any<object>(), Arg.Any<DeliveryOptions?>());
        bus.DidNotReceive().EndpointFor(Arg.Any<string>());
    }

    [Test]
    public async Task DispatchAsync_PublishesUnshardedSendAsync_WhenShardCountIsOne()
    {
        var bus = Substitute.For<IMessageBus>();
        var pendingRepo = Substitute.For<IPendingEcommerceOrderRepository>();
        pendingRepo.FindByExternalIdAsync(
                Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((PendingEcommerceOrder?)null);
        var dispatcher = new IngestionDispatcher(bus, pendingRepo, BuildConfig(shardCount: null));

        var ack = await dispatcher.DispatchAsync(Payload(TenantId.New(), "ORD-NEW"), CancellationToken.None);

        ack.AlreadyPending.Should().BeFalse();
        ack.PendingOrderId.Should().NotBe(Guid.Empty);
        await bus.Received(1).SendAsync(
            Arg.Is<IngestEcommerceOrderCommand>(c => c.PreallocatedPendingId == ack.PendingOrderId),
            Arg.Any<DeliveryOptions?>());
        bus.DidNotReceive().EndpointFor(Arg.Any<string>());
    }

    [Test]
    public async Task DispatchAsync_TargetsShardEndpoint_WhenShardCountAboveOne()
    {
        const int shardCount = 4;
        var tenantGuid = Guid.NewGuid();
        var expectedShard = IngestionDispatcher.ResolveShard(tenantGuid, shardCount);
        var expectedQueue = $"IngestEcommerceOrderCommand-shard-{expectedShard}";

        var bus = Substitute.For<IMessageBus>();
        var endpoint = Substitute.For<IDestinationEndpoint>();
        bus.EndpointFor(Arg.Any<string>()).Returns(endpoint);
        var pendingRepo = Substitute.For<IPendingEcommerceOrderRepository>();
        pendingRepo.FindByExternalIdAsync(
                Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((PendingEcommerceOrder?)null);
        var dispatcher = new IngestionDispatcher(bus, pendingRepo, BuildConfig(shardCount));

        var ack = await dispatcher.DispatchAsync(Payload(new TenantId(tenantGuid), "ORD-A"), CancellationToken.None);

        ack.AlreadyPending.Should().BeFalse();
        bus.Received(1).EndpointFor(expectedQueue);
        await endpoint.Received(1).SendAsync(
            Arg.Is<IngestEcommerceOrderCommand>(c => c.PreallocatedPendingId == ack.PendingOrderId),
            Arg.Any<DeliveryOptions?>());
        await bus.DidNotReceive().SendAsync(Arg.Any<object>(), Arg.Any<DeliveryOptions?>());
    }

    [Test]
    public async Task DispatchAsync_PinsSameTenant_ToSameShardAcrossCalls()
    {
        const int shardCount = 6;
        var tenantId = new TenantId(Guid.NewGuid());

        var bus = Substitute.For<IMessageBus>();
        var endpoint = Substitute.For<IDestinationEndpoint>();
        bus.EndpointFor(Arg.Any<string>()).Returns(endpoint);
        var pendingRepo = Substitute.For<IPendingEcommerceOrderRepository>();
        pendingRepo.FindByExternalIdAsync(
                Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((PendingEcommerceOrder?)null);
        var dispatcher = new IngestionDispatcher(bus, pendingRepo, BuildConfig(shardCount));

        await dispatcher.DispatchAsync(Payload(tenantId, "ORD-1"), CancellationToken.None);
        await dispatcher.DispatchAsync(Payload(tenantId, "ORD-2"), CancellationToken.None);

        var queueCalls = bus.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IMessageBus.EndpointFor))
            .Select(c => (string)c.GetArguments()[0]!)
            .ToArray();
        queueCalls.Should().HaveCount(2);
        queueCalls[0].Should().Be(queueCalls[1]);
    }

    private static IConfiguration BuildConfig(int? shardCount)
    {
        var values = new Dictionary<string, string?>();
        if (shardCount is not null)
            values["Messaging:IngestionShardCount"] = shardCount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static EcommerceOrderPayload Payload(TenantId tenant, string externalOrderId)
    {
        var origin = new Address("Origin", "Line 1", null, "Hoofddorp", null, "2132 AA", new CountryCode("NL"));
        var dest = new Address("Customer", "Line 1", null, "Amsterdam", null, "1012 LM", new CountryCode("NL"));
        return new EcommerceOrderPayload(
            TenantId: tenant,
            ConnectorCode: "shopify",
            ExternalOrderId: externalOrderId,
            Currency: "EUR",
            From: origin,
            To: dest,
            Items: [new EcommerceOrderLineItem("SKU", "Widget", 1, new Money(20m, "EUR"), Weight.FromGrams(500))],
            TotalWeight: Weight.FromGrams(500),
            PackageDimensions: new Dimension(100, 100, 50));
    }
}
