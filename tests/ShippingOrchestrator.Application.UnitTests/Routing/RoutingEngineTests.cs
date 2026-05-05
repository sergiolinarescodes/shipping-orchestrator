using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Application.Routing;
using ShippingOrchestrator.Application.Routing.Rules;
using ShippingOrchestrator.Domain.Connections;
using ShippingOrchestrator.Domain.Shipments;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Domain.ValueObjects;

namespace ShippingOrchestrator.Application.UnitTests.Routing;

[TestFixture]
public class RoutingEngineTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Test]
    public async Task Selects_carrier_that_covers_origin_and_destination()
    {
        var tenant = TenantId.New();
        var nl = new CountryCode("NL");
        var be = new CountryCode("BE");
        var de = new CountryCode("DE");

        var postnl = CarrierAssignment.Create(tenant, "postnl", priority: 10,
            origins: [nl, be], destinations: [new CountryCode(CountryCode.Wildcard)], Now);
        var nonViable = CarrierAssignment.Create(tenant, "carrierx", priority: 99,
            origins: [de], destinations: [de], Now);

        var assignments = Substitute.For<ICarrierAssignmentRepository>();
        assignments.ListForTenantAsync(tenant, Arg.Any<CancellationToken>())
            .Returns(new[] { postnl, nonViable });

        var engine = new RoutingEngine(
            new ICarrierRoutingRule[] { new CountryAllowedRule(), new PriorityRule() },
            assignments);

        var shipment = Shipment.Create(tenant, batchId: null,
            from: NewAddress("NL"), to: NewAddress("DE"),
            parcel: new Parcel(Weight.FromGrams(500),
                new Dimension(100, 100, 100), new Money(10m, "EUR")),
            preferredService: null, now: Now);

        var decision = await engine.SelectCarrierAsync(shipment, CancellationToken.None);

        decision.Should().NotBeNull();
        decision.CarrierCode.Should().Be("postnl");
        decision.AppliedRuleAttributions.Should().NotBeEmpty();
    }

    [Test]
    public async Task Returns_null_when_no_assignment_covers_route()
    {
        var tenant = TenantId.New();
        var assignments = Substitute.For<ICarrierAssignmentRepository>();
        assignments.ListForTenantAsync(tenant, Arg.Any<CancellationToken>())
            .Returns([CarrierAssignment.Create(tenant, "carrierx", 0,
                origins: [new CountryCode("DE")], destinations: [new CountryCode("DE")], Now)]);

        var engine = new RoutingEngine(
            new ICarrierRoutingRule[] { new CountryAllowedRule() }, assignments);

        var shipment = Shipment.Create(tenant, null,
            NewAddress("NL"), NewAddress("BE"),
            new Parcel(Weight.FromGrams(500), new Dimension(100, 100, 100), new Money(10m, "EUR")),
            null, Now);

        (await engine.SelectCarrierAsync(shipment, CancellationToken.None)).Should().BeNull();
    }

    private static Address NewAddress(string countryIsoAlpha2) => new(
        Name: "Acme",
        Line1: "Hoofdstraat 1",
        Line2: null,
        City: "Amsterdam",
        Region: null,
        PostalCode: "1012 AB",
        Country: new CountryCode(countryIsoAlpha2));
}
