using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Application.Ingestion;
using ShippingOrchestrator.Domain.Events;
using ShippingOrchestrator.Domain.Ingestion;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Domain.ValueObjects;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;
using IngestionReasonCode = ShippingOrchestrator.Domain.Ingestion.IngestionReasonCode;

namespace ShippingOrchestrator.Application.UnitTests.Ingestion;

[TestFixture]
public class IngestEcommerceOrderHandlerTests
{
    private static EcommerceOrderPayload Payload(string externalOrderId, string currency = "EUR", TenantId? tenantId = null)
    {
        var origin = new Address("Origin", "Line 1", null, "Hoofddorp", null, "2132 AA", new CountryCode("NL"));
        var dest = new Address("Customer", "Line 1", null, "Amsterdam", null, "1012 LM", new CountryCode("NL"));
        return new EcommerceOrderPayload(
            TenantId: tenantId ?? TenantId.New(),
            ConnectorCode: "shopify",
            ExternalOrderId: externalOrderId,
            Currency: currency,
            From: origin,
            To: dest,
            Items: [new EcommerceOrderLineItem("SKU", "Acme widget", 1, new Money(20m, currency), Weight.FromGrams(800))],
            TotalWeight: Weight.FromGrams(800),
            PackageDimensions: new Dimension(200, 200, 100),
            Reference: externalOrderId);
    }

    private static IClock ClockAt(DateTimeOffset now)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now);
        return clock;
    }

    private static IIngestionFailureRepository FailureRepoReturning(IngestionFailure? value)
    {
        var repo = Substitute.For<IIngestionFailureRepository>();
        repo.FindOpenByExternalOrderIdAsync(
                Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(value);
        return repo;
    }

    [Test]
    public async Task Handler_persists_pending_order_when_none_exists()
    {
        var pendingRepo = Substitute.For<IPendingEcommerceOrderRepository>();
        pendingRepo.FindByExternalIdAsync(
                Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((PendingEcommerceOrder?)null);

        var payload = Payload("ORDER-123");
        var failureRepo = FailureRepoReturning(null);
        var uow = Substitute.For<IUnitOfWork>();
        var clock = ClockAt(DateTimeOffset.UtcNow);

        var result = await IngestEcommerceOrderHandler.Handle(
            new IngestEcommerceOrderCommand(payload, Guid.Empty), pendingRepo, failureRepo, uow, clock, CancellationToken.None);

        result.AlreadyPending.Should().BeFalse();
        result.PendingOrderId.Should().NotBe(Guid.Empty);

        await pendingRepo.Received(1).AddAsync(
            Arg.Is<PendingEcommerceOrder>(p =>
                p.TenantId == payload.TenantId
                && p.PlatformCode == "shopify"
                && p.ExternalOrderId == "ORDER-123"
                && p.PayloadJson.Contains("ORDER-123")),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handler_returns_existing_pending_id_when_replayed()
    {
        var pendingRepo = Substitute.For<IPendingEcommerceOrderRepository>();
        var payload = Payload("ORDER-456");
        var existingId = Guid.NewGuid();
        var existing = new PendingEcommerceOrder(
            existingId, payload.TenantId, payload.ConnectorCode, payload.ExternalOrderId, "{}", DateTimeOffset.UtcNow);
        pendingRepo.FindByExternalIdAsync(
                payload.TenantId, payload.ConnectorCode, payload.ExternalOrderId, Arg.Any<CancellationToken>())
            .Returns(existing);

        var failureRepo = FailureRepoReturning(null);
        var uow = Substitute.For<IUnitOfWork>();
        var clock = ClockAt(DateTimeOffset.UtcNow);

        var result = await IngestEcommerceOrderHandler.Handle(
            new IngestEcommerceOrderCommand(payload, Guid.Empty), pendingRepo, failureRepo, uow, clock, CancellationToken.None);

        result.AlreadyPending.Should().BeTrue();
        result.PendingOrderId.Should().Be(existingId);
        await pendingRepo.DidNotReceive().AddAsync(Arg.Any<PendingEcommerceOrder>(), Arg.Any<CancellationToken>());
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handler_resolves_open_failure_on_new_pending_path()
    {
        var tenantId = TenantId.New();
        var payload = Payload("ORDER-789", tenantId: tenantId);
        var now = DateTimeOffset.UtcNow;

        var openFailure = IngestionFailure.Raise(
            tenantId, "shopify", "ORDER-789",
            IngestionReasonCode.MissingShippingAddress, "missing address", "Add an address.",
            "{}", "abc123", null, IngestionFailureSeverity.Warning, now.AddMinutes(-2));
        openFailure.ClearDomainEvents();

        var pendingRepo = Substitute.For<IPendingEcommerceOrderRepository>();
        pendingRepo.FindByExternalIdAsync(tenantId, "shopify", "ORDER-789", Arg.Any<CancellationToken>())
            .Returns((PendingEcommerceOrder?)null);
        var failureRepo = FailureRepoReturning(openFailure);
        var uow = Substitute.For<IUnitOfWork>();
        var clock = ClockAt(now);

        var result = await IngestEcommerceOrderHandler.Handle(
            new IngestEcommerceOrderCommand(payload, Guid.Empty), pendingRepo, failureRepo, uow, clock, CancellationToken.None);

        result.AlreadyPending.Should().BeFalse();
        openFailure.Status.Should().Be(IngestionFailureStatus.Resolved);
        openFailure.ResolvedAt.Should().Be(now);
        openFailure.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<IngestionFailureResolved>();
        await failureRepo.Received(1).FindOpenByExternalOrderIdAsync(
            tenantId, "shopify", "ORDER-789", Arg.Any<CancellationToken>());
        await pendingRepo.Received(1).AddAsync(Arg.Any<PendingEcommerceOrder>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handler_resolves_open_failure_on_already_pending_path()
    {
        var tenantId = TenantId.New();
        var payload = Payload("ORDER-999", tenantId: tenantId);
        var now = DateTimeOffset.UtcNow;

        var existingPending = new PendingEcommerceOrder(
            Guid.NewGuid(), tenantId, payload.ConnectorCode, payload.ExternalOrderId, "{}", now.AddMinutes(-1));
        var openFailure = IngestionFailure.Raise(
            tenantId, "shopify", "ORDER-999",
            IngestionReasonCode.ZeroWeight, "weight 0g", "Set weight on the line item.",
            "{}", "deadbeef", null, IngestionFailureSeverity.Warning, now.AddMinutes(-2));
        openFailure.ClearDomainEvents();

        var pendingRepo = Substitute.For<IPendingEcommerceOrderRepository>();
        pendingRepo.FindByExternalIdAsync(tenantId, "shopify", "ORDER-999", Arg.Any<CancellationToken>())
            .Returns(existingPending);
        var failureRepo = FailureRepoReturning(openFailure);
        var uow = Substitute.For<IUnitOfWork>();
        var clock = ClockAt(now);

        var result = await IngestEcommerceOrderHandler.Handle(
            new IngestEcommerceOrderCommand(payload, Guid.Empty), pendingRepo, failureRepo, uow, clock, CancellationToken.None);

        result.AlreadyPending.Should().BeTrue();
        result.PendingOrderId.Should().Be(existingPending.Id);
        openFailure.Status.Should().Be(IngestionFailureStatus.Resolved);
        await pendingRepo.DidNotReceive().AddAsync(Arg.Any<PendingEcommerceOrder>(), Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handler_lowercases_connector_code_for_failure_lookup()
    {
        // Failures are stored with ConnectorCode.ToLowerInvariant() (see RecordIngestionFailureHandler).
        // The lookup must mirror that normalization or the auto-resolve will silently miss.
        var tenantId = TenantId.New();
        var payload = Payload("ORDER-MIXED", tenantId: tenantId)
            with { ConnectorCode = "Shopify" };

        var pendingRepo = Substitute.For<IPendingEcommerceOrderRepository>();
        pendingRepo.FindByExternalIdAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((PendingEcommerceOrder?)null);
        var failureRepo = FailureRepoReturning(null);
        var uow = Substitute.For<IUnitOfWork>();
        var clock = ClockAt(DateTimeOffset.UtcNow);

        await IngestEcommerceOrderHandler.Handle(
            new IngestEcommerceOrderCommand(payload, Guid.Empty), pendingRepo, failureRepo, uow, clock, CancellationToken.None);

        await failureRepo.Received(1).FindOpenByExternalOrderIdAsync(
            tenantId, "shopify", "ORDER-MIXED", Arg.Any<CancellationToken>());
    }
}
