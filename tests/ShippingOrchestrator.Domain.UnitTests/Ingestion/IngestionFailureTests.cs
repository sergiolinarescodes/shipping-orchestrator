using FluentAssertions;
using NUnit.Framework;
using ShippingOrchestrator.Domain.Events;
using ShippingOrchestrator.Domain.Ingestion;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Domain.UnitTests.Ingestion;

[TestFixture]
public class IngestionFailureTests
{
    private static readonly TenantId Tenant = TenantId.New();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private const string ConnectorCode = "shopify";
    private const string Hash = "deadbeef";

    private static IngestionFailure NewOpen(string? externalOrderId = "ORDER-1") =>
        IngestionFailure.Raise(
            Tenant, ConnectorCode, externalOrderId,
            IngestionReasonCode.MissingShippingAddress,
            "missing address",
            "Add a shipping address.",
            rawBodyExcerpt: "{\"id\":1}",
            rawBodyHash: Hash,
            contextJson: null,
            severity: IngestionFailureSeverity.Warning,
            now: Now);

    [Test]
    public void Raise_creates_open_failure_with_event_and_initial_count()
    {
        var failure = NewOpen();

        failure.Status.Should().Be(IngestionFailureStatus.Open);
        failure.OccurrenceCount.Should().Be(1);
        failure.LookupKey.Should().Be("ORDER-1");
        failure.OccurredAt.Should().Be(Now);
        failure.LastOccurredAt.Should().Be(Now);
        failure.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IngestionFailureRaised>();
    }

    [Test]
    public void Raise_without_external_id_falls_back_to_hash_lookup_key()
    {
        var failure = NewOpen(externalOrderId: null);

        failure.LookupKey.Should().Be($"hash:{Hash}");
        failure.ExternalOrderId.Should().BeNull();
    }

    [Test]
    public void Reoccur_bumps_count_and_raises_event()
    {
        var failure = NewOpen();
        failure.ClearDomainEvents();

        var later = Now.AddMinutes(5);
        failure.Reoccur(IngestionReasonCode.ZeroWeight, "weight is zero", "Set product weights.", null, later);

        failure.OccurrenceCount.Should().Be(2);
        failure.ReasonCode.Should().Be(IngestionReasonCode.ZeroWeight);
        failure.LastOccurredAt.Should().Be(later);
        failure.OccurredAt.Should().Be(Now);
        failure.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IngestionFailureReoccurred>();
    }

    [Test]
    public void BumpOccurrenceCount_updates_state_silently()
    {
        var failure = NewOpen();
        failure.ClearDomainEvents();

        var later = Now.AddSeconds(10);
        failure.BumpOccurrenceCount(later);

        failure.OccurrenceCount.Should().Be(2);
        failure.LastOccurredAt.Should().Be(later);
        failure.DomainEvents.Should().BeEmpty();
    }

    [Test]
    public void Resolve_marks_resolved_sets_expiry_and_raises_event()
    {
        var failure = NewOpen();
        failure.ClearDomainEvents();

        var later = Now.AddHours(1);
        failure.Resolve("re-ingested", later);

        failure.Status.Should().Be(IngestionFailureStatus.Resolved);
        failure.ResolvedAt.Should().Be(later);
        failure.ResolvedReason.Should().Be("re-ingested");
        failure.ExpiresAt.Should().Be(later.AddDays(30));
        failure.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IngestionFailureResolved>();
    }

    [Test]
    public void Resolve_is_idempotent()
    {
        var failure = NewOpen();
        failure.Resolve("first", Now.AddMinutes(1));
        failure.ClearDomainEvents();

        failure.Resolve("second", Now.AddMinutes(2));

        failure.ResolvedReason.Should().Be("first");
        failure.DomainEvents.Should().BeEmpty();
    }

    [Test]
    public void Reoccur_on_resolved_failure_throws()
    {
        var failure = NewOpen();
        failure.Resolve("first", Now.AddMinutes(1));

        var act = () => failure.Reoccur(IngestionReasonCode.ZeroWeight, "x", "y", null, Now.AddMinutes(2));

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void Dismiss_marks_dismissed_and_blocks_resolve()
    {
        var failure = NewOpen();
        failure.Dismiss("staff-user", Now.AddMinutes(1));

        failure.Status.Should().Be(IngestionFailureStatus.Dismissed);
        failure.DismissedBy.Should().Be("staff-user");

        var act = () => failure.Resolve("re-ingested", Now.AddMinutes(2));
        act.Should().Throw<InvalidOperationException>();
    }
}
