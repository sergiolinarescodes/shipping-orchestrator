using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ShippingOrchestrator.Domain.ValueObjects;
using ShippingOrchestrator.E2E.Tests.Infrastructure;
using ShippingOrchestrator.Modules.Abstractions;
using ShippingOrchestrator.Modules.Abstractions.Carriers;

namespace ShippingOrchestrator.E2E.Tests;

/// <summary>
/// Walks the rate-quote path end-to-end through the connector registry: resolves the
/// registered "postnl" carrier from composite-host DI and exercises
/// <see cref="ICarrierConnector.QuoteAsync"/>. The production saga does not call QuoteAsync
/// today, so the read-side cannot cover this — only this fixture asserts the registry →
/// factory → simulator chain stays wired.
/// </summary>
[TestFixture]
public class QuotePathTests : E2ETestBase
{
    [Test]
    public async Task PostNL_quote_returns_one_priced_option_via_registry()
    {
        using var scope = E2EFixture.Current.App.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<ConnectorRegistry>();
        var registration = registry.Get("postnl");
        registration.Kind.Should().Be(ConnectorKind.Carrier);
        registration.Metadata!["mode"].Should().Be("inmemory");

        var carrier = (ICarrierConnector)registration.ConnectorFactory(scope.ServiceProvider);
        var request = new RateQuoteRequest(
            From: NewAddress("NL"),
            To: NewAddress("BE"),
            Parcel: new Parcel(
                Weight: Weight.FromGrams(750),
                Dimensions: new Dimension(200, 200, 100),
                DeclaredValue: new Money(24.99m, "EUR")),
            PreferredService: ServiceLevel.Standard);

        var result = await carrier.QuoteAsync(request, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Options.Should().HaveCount(1);
        var option = result.Options[0];
        option.CarrierServiceCode.Should().Be("PNL_3085");
        option.ServiceLevel.Should().Be(ServiceLevel.Standard);
        option.Price.Currency.Should().Be("EUR");
        option.Price.Amount.Should().BeApproximately(8.025m, 0.001m); // 7.95 + 0.10 * 0.75kg
        option.EstimatedTransitTime.Should().Be(TimeSpan.FromDays(1));
    }

    private static Address NewAddress(string country) => new(
        Name: "Acme",
        Line1: "Hoofdstraat 1",
        Line2: null,
        City: "Amsterdam",
        Region: null,
        PostalCode: "1012 AB",
        Country: new CountryCode(country));
}
