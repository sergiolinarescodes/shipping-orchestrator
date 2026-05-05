using FluentAssertions;
using NUnit.Framework;
using ShippingOrchestrator.Domain.Shipments;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Domain.ValueObjects;

namespace ShippingOrchestrator.Domain.UnitTests.Shipments;

[TestFixture]
public class ShipmentTrackingTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static Shipment NewLabeledShipment()
    {
        var shipment = Shipment.Create(
            TenantId.New(),
            batchId: Guid.NewGuid(),
            from: TestAddress("NL"),
            to: TestAddress("BE"),
            parcel: new Parcel(Weight.FromGrams(800), new Dimension(200, 200, 100), new Money(20m, "EUR")),
            preferredService: ServiceLevel.Standard,
            now: Now);
        shipment.SelectCarrier("postnl", Now);
        shipment.MarkLabelRequested(Now);
        shipment.RecordLabel("3SAB1234567", "https://mock.postnl.local/labels/x.pdf", Now);
        return shipment;
    }

    private static Address TestAddress(string country) => new(
        Name: "Test", Line1: "Street 1", Line2: null, City: "Amsterdam",
        Region: null, PostalCode: "1012 AB", Country: new CountryCode(country));

    [Test]
    public void Append_adds_unique_events_and_dedupes()
    {
        var shipment = NewLabeledShipment();
        var t = Now;
        var first = new[]
        {
            new ShipmentTrackingUpdate("Accepted", "at depot", "Hoofddorp", t),
            new ShipmentTrackingUpdate("InTransit", "on route", "Amsterdam", t.AddMinutes(60)),
        };

        shipment.AppendTrackingEvents(first, Now).Should().BeTrue();
        shipment.TrackingEvents.Should().HaveCount(2);

        // Reapplying the same events is a no-op.
        shipment.AppendTrackingEvents(first, Now).Should().BeFalse();
        shipment.TrackingEvents.Should().HaveCount(2);
    }

    [Test]
    public void Latest_event_promotes_status_in_transit()
    {
        var shipment = NewLabeledShipment();
        shipment.AppendTrackingEvents(
            new[] { new ShipmentTrackingUpdate("InTransit", null, "Hub", Now.AddMinutes(5)) },
            Now);
        shipment.Status.Should().Be(ShipmentStatus.InTransit);
    }

    [Test]
    public void Delivered_event_promotes_to_delivered()
    {
        var shipment = NewLabeledShipment();
        shipment.AppendTrackingEvents(
            new[]
            {
                new ShipmentTrackingUpdate("InTransit", null, "Hub", Now.AddMinutes(5)),
                new ShipmentTrackingUpdate("Delivered", "Left at door", "Customer", Now.AddHours(8)),
            },
            Now);
        shipment.Status.Should().Be(ShipmentStatus.Delivered);
    }
}
