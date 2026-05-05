using FluentAssertions;
using NUnit.Framework;

namespace ShippingOrchestrator.Architecture.Tests;

/// <summary>
/// The Domain enum and the Modules.Abstractions enum are intentional duplicates so connectors
/// can throw <c>IngestionTranslationException</c> without taking a Domain dependency. The
/// webhook seam casts between them by integer value, so member names + ordinal values must
/// stay in lockstep. This test fails if anyone adds a member to one side without the other.
/// </summary>
[TestFixture]
public class IngestionReasonCodeParityTests
{
    [Test]
    public void Domain_and_Abstractions_enums_have_identical_members_and_ordinals()
    {
        var domain = typeof(Domain.Ingestion.IngestionReasonCode);
        var abstractions = typeof(Modules.Abstractions.Ecommerce.IngestionReasonCode);

        var domainPairs = Enum.GetValues(domain)
            .Cast<object>()
            .Select(v => (Name: Enum.GetName(domain, v)!, Value: (int)v))
            .OrderBy(p => p.Value)
            .ToArray();

        var abstractionsPairs = Enum.GetValues(abstractions)
            .Cast<object>()
            .Select(v => (Name: Enum.GetName(abstractions, v)!, Value: (int)v))
            .OrderBy(p => p.Value)
            .ToArray();

        abstractionsPairs.Should().BeEquivalentTo(
            domainPairs,
            options => options.WithStrictOrdering(),
            "Domain.Ingestion.IngestionReasonCode and Modules.Abstractions.Ecommerce.IngestionReasonCode must mirror each other exactly — webhook endpoint casts between them by integer value.");
    }
}
