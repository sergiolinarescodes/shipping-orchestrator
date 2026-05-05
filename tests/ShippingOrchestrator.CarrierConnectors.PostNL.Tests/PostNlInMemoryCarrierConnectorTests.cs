using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using ShippingOrchestrator.Domain.ValueObjects;
using ShippingOrchestrator.Modules.Abstractions.Carriers;

namespace ShippingOrchestrator.CarrierConnectors.PostNL.Tests;

[TestFixture]
public class PostNlInMemoryCarrierConnectorTests
{
    private static PostNlInMemoryCarrierConnector NewConnector(double failureProbability = 0.0) => new(
        Options.Create(new PostNlInMemoryOptions
        {
            MinLatencyMs = 10,
            MaxLatencyMs = 25,
            FailureProbability = failureProbability,
            LabelStorageBaseUri = "https://mock.local/labels",
        }),
        NullLogger<PostNlInMemoryCarrierConnector>.Instance);

    [Test]
    public async Task CreateLabel_returns_tracking_and_uri_on_success()
    {
        var connector = NewConnector();
        var request = new LabelCreationRequest(
            ShipmentId: Guid.NewGuid(),
            From: NewAddress("NL"),
            To: NewAddress("BE"),
            Parcel: new Parcel(Weight.FromGrams(750), new Dimension(200, 200, 100), new Money(20m, "EUR")),
            PreferredService: ServiceLevel.Standard,
            Reference: null);

        var result = await connector.CreateLabelAsync(request, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TrackingNumber.Should().NotBeNullOrEmpty();
        result.LabelUri.Should().StartWith("https://mock.local/labels/");
        result.ChargedAmount!.Value.Currency.Should().Be("EUR");
    }

    [Test]
    public async Task CreateLabel_returns_failure_when_probability_is_one()
    {
        var connector = NewConnector(failureProbability: 1.0);
        var request = new LabelCreationRequest(
            ShipmentId: Guid.NewGuid(),
            From: NewAddress("NL"),
            To: NewAddress("DE"),
            Parcel: new Parcel(Weight.FromGrams(500), new Dimension(100, 100, 100), new Money(10m, "EUR")),
            PreferredService: null,
            Reference: null);

        var result = await connector.CreateLabelAsync(request, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("forced failure");
    }

    [Test]
    public async Task QuoteAsync_returns_one_option()
    {
        var connector = NewConnector();
        var quote = await connector.QuoteAsync(
            new RateQuoteRequest(NewAddress("NL"), NewAddress("BE"),
                new Parcel(Weight.FromGrams(500), new Dimension(100, 100, 100), new Money(10m, "EUR")),
                ServiceLevel.Standard),
            CancellationToken.None);

        quote.Success.Should().BeTrue();
        quote.Options.Should().HaveCount(1);
    }

    private static Address NewAddress(string country) => new(
        "Acme", "Line 1", null, "Amsterdam", null, "1012 AB", new CountryCode(country));
}
