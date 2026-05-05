using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Application.Ingestion;
using ShippingOrchestrator.Domain.Ingestion;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Application.UnitTests.Ingestion;

[TestFixture]
public class RecordIngestionFailureHandlerTests
{
    private static readonly TenantId Tenant = TenantId.New();

    private static RecordIngestionFailureCommand Command(
        string? externalOrderId = "ORDER-1",
        IngestionReasonCode code = IngestionReasonCode.MissingShippingAddress) =>
        new(
            Tenant,
            ConnectorCode: "shopify",
            ExternalOrderId: externalOrderId,
            ReasonCode: code,
            Message: "missing address",
            TenantHint: "Add an address.",
            RawBodyExcerpt: "{}",
            RawBodyHash: "abc123",
            Context: null,
            Severity: IngestionFailureSeverity.Warning);

    private static IClock ClockAt(DateTimeOffset now)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now);
        return clock;
    }

    [Test]
    public async Task Handle_creates_new_open_row_when_no_existing()
    {
        var repo = Substitute.For<IIngestionFailureRepository>();
        repo.FindOpenByLookupKeyAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((IngestionFailure?)null);
        var uow = Substitute.For<IUnitOfWork>();
        var clock = ClockAt(DateTimeOffset.UtcNow);

        var result = await RecordIngestionFailureHandler.Handle(
            Command(), repo, uow, clock, CancellationToken.None);

        result.WasNew.Should().BeTrue();
        result.OccurrenceCount.Should().Be(1);
        await repo.Received(1).AddAsync(
            Arg.Is<IngestionFailure>(f =>
                f.TenantId == Tenant
                && f.ConnectorCode == "shopify"
                && f.ReasonCode == IngestionReasonCode.MissingShippingAddress
                && f.Status == IngestionFailureStatus.Open),
            Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_reoccurs_when_existing_open_and_cooldown_elapsed()
    {
        var existing = IngestionFailure.Raise(
            Tenant, "shopify", "ORDER-1",
            IngestionReasonCode.MissingShippingAddress, "msg", "hint",
            "{}", "abc123", null, IngestionFailureSeverity.Warning,
            DateTimeOffset.UtcNow.AddMinutes(-5));
        existing.ClearDomainEvents();

        var repo = Substitute.For<IIngestionFailureRepository>();
        repo.FindOpenByLookupKeyAsync(Tenant, "shopify", "ORDER-1", Arg.Any<CancellationToken>())
            .Returns(existing);
        var uow = Substitute.For<IUnitOfWork>();
        var clock = ClockAt(DateTimeOffset.UtcNow);

        var result = await RecordIngestionFailureHandler.Handle(
            Command(), repo, uow, clock, CancellationToken.None);

        result.WasNew.Should().BeFalse();
        result.OccurrenceCount.Should().Be(2);
        existing.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Domain.Events.IngestionFailureReoccurred>();
        await repo.DidNotReceive().AddAsync(Arg.Any<IngestionFailure>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_silently_bumps_count_when_in_cooldown_and_reason_unchanged()
    {
        var now = DateTimeOffset.UtcNow;
        var existing = IngestionFailure.Raise(
            Tenant, "shopify", "ORDER-1",
            IngestionReasonCode.MissingShippingAddress, "msg", "hint",
            "{}", "abc123", null, IngestionFailureSeverity.Warning,
            now.AddSeconds(-10));
        existing.ClearDomainEvents();

        var repo = Substitute.For<IIngestionFailureRepository>();
        repo.FindOpenByLookupKeyAsync(Tenant, "shopify", "ORDER-1", Arg.Any<CancellationToken>())
            .Returns(existing);
        var uow = Substitute.For<IUnitOfWork>();
        var clock = ClockAt(now);

        var result = await RecordIngestionFailureHandler.Handle(
            Command(), repo, uow, clock, CancellationToken.None);

        result.WasNew.Should().BeFalse();
        result.OccurrenceCount.Should().Be(2);
        existing.DomainEvents.Should().BeEmpty(
            "cooldown should keep the bus quiet when the same reason recurs within 60s");
    }

    [Test]
    public async Task Handle_emits_event_in_cooldown_when_reason_changed()
    {
        var now = DateTimeOffset.UtcNow;
        var existing = IngestionFailure.Raise(
            Tenant, "shopify", "ORDER-1",
            IngestionReasonCode.MissingShippingAddress, "msg", "hint",
            "{}", "abc123", null, IngestionFailureSeverity.Warning,
            now.AddSeconds(-10));
        existing.ClearDomainEvents();

        var repo = Substitute.For<IIngestionFailureRepository>();
        repo.FindOpenByLookupKeyAsync(Tenant, "shopify", "ORDER-1", Arg.Any<CancellationToken>())
            .Returns(existing);
        var uow = Substitute.For<IUnitOfWork>();
        var clock = ClockAt(now);

        await RecordIngestionFailureHandler.Handle(
            Command(code: IngestionReasonCode.ZeroWeight), repo, uow, clock, CancellationToken.None);

        existing.ReasonCode.Should().Be(IngestionReasonCode.ZeroWeight);
        existing.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Domain.Events.IngestionFailureReoccurred>(
                "reason change must always raise an event so the projection picks it up");
    }

    [Test]
    public async Task Handle_uses_hash_lookup_when_external_id_missing()
    {
        var repo = Substitute.For<IIngestionFailureRepository>();
        repo.FindOpenByLookupKeyAsync(Tenant, "shopify", "hash:abc123", Arg.Any<CancellationToken>())
            .Returns((IngestionFailure?)null);
        var uow = Substitute.For<IUnitOfWork>();
        var clock = ClockAt(DateTimeOffset.UtcNow);

        var result = await RecordIngestionFailureHandler.Handle(
            Command(externalOrderId: null, code: IngestionReasonCode.ParseError),
            repo, uow, clock, CancellationToken.None);

        result.WasNew.Should().BeTrue();
        await repo.Received(1).FindOpenByLookupKeyAsync(
            Tenant, "shopify", "hash:abc123", Arg.Any<CancellationToken>());
    }
}
