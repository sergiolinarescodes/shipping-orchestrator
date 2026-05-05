using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NUnit.Framework;
using ShippingOrchestrator.E2E.Tests.Infrastructure;
using ShippingOrchestrator.PrivateApi.Endpoints;
using ShippingOrchestrator.ReadModels.Abstractions.Customer;

namespace ShippingOrchestrator.E2E.Tests;

[TestFixture]
public class HappyPathParcelTests : E2ETestBase
{
    [Test]
    public async Task Onboard_then_simulate_order_yields_labeled_shipment_with_tracking_events()
    {
        // Tenant-only ops handoff + tenant-driven Shopify install via the dashboard flow.
        var tenantId = await TenantBootstrap.CreateTenantAsync("Acme NL");
        await TenantBootstrap.AssignPostNlAsync(tenantId);
        await TenantBootstrap.InstallShopifyAsync(tenantId, "acme-nl.myshopify.com");

        // Send a simulated parcel through the same path a real Shopify webhook would use:
        // ingest -> persisted as pending. The customer then bundles the pending order into
        // a shipment batch, which is what the dashboard "Bundle N orders" button does.
        var simResp = await StaffPost($"/admin/tenants/{tenantId}/simulate-order",
            new { originCountry = "NL", destinationCountry = "NL", weightGrams = 1000, description = "Acme widget" });
        simResp.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var sim = await simResp.Content.ReadFromJsonAsync<SimulateOrderResponse>();
        sim.Should().NotBeNull();
        sim!.AlreadyPending.Should().BeFalse();

        await WaitForPendingOrderAsync(tenantId, sim.PendingOrderId);

        var bundleResp = await TenantPost(tenantId, "/v1/dashboard/orders/bundle",
            new { orderIds = new[] { sim.PendingOrderId } });
        bundleResp.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var bundle = await bundleResp.Content.ReadFromJsonAsync<BundleOrdersResult>();
        bundle.Should().NotBeNull();

        // Wait for the batch to flip to Completed via the existing test signal.
        var completion = await E2EFixture.Current.BatchSignal.WaitAsync(bundle!.BatchId, TimeSpan.FromSeconds(30));
        completion.SuccessCount.Should().Be(1);

        // Customer dashboard sees the shipment Labeled with carrier metadata.
        var shipmentId = bundle.ShipmentIds[0];
        var customerView = await PollUntilLabeled(tenantId, shipmentId);
        customerView.CarrierCode.Should().Be("postnl");
        customerView.TrackingNumber.Should().NotBeNullOrEmpty();
        customerView.LabelUri.Should().StartWith("https://mock.postnl.local/labels/");

        // Tracking events arrive within the polling window (the in-memory PostNL connector
        // synthesises Accepted + InTransit on every TrackAsync call).
        var withEvents = await PollForTrackingEvents(tenantId, shipmentId);
        withEvents.Events.Should().NotBeNullOrEmpty();
        withEvents.Events!.Should().Contain(e => e.EventCode == "InTransit");
    }

    private static async Task<CustomerShipmentView> PollUntilLabeled(Guid tenantId, Guid shipmentId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        CustomerShipmentView? view = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var resp = await TenantGet(tenantId, $"/v1/shipments/{shipmentId}");
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                view = await resp.Content.ReadFromJsonAsync<CustomerShipmentView>();
                if (view is not null && view.Status is "Labeled" or "InTransit" or "Delivered") return view;
            }
            await Task.Delay(100);
        }
        Assert.Fail($"Shipment never reached Labeled (last status: {view?.Status ?? "<missing>"}).");
        return view!;
    }

    private static async Task<CustomerShipmentView> PollForTrackingEvents(Guid tenantId, Guid shipmentId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        CustomerShipmentView? view = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var resp = await TenantGet(tenantId, $"/v1/shipments/{shipmentId}");
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                view = await resp.Content.ReadFromJsonAsync<CustomerShipmentView>();
                if (view?.Events is { Count: > 0 }) return view;
            }
            await Task.Delay(200);
        }
        Assert.Fail($"No tracking events appeared in 20s (events: {view?.Events?.Count ?? 0}).");
        return view!;
    }

    private static Task<HttpResponseMessage> StaffPost(string url, object? body) =>
        HttpHelpers.SendAsync(HttpMethod.Post, url, body,
            ("X-Staff-Role", "admin"), ("X-Staff-User", "ops-tester"));

    private static Task<HttpResponseMessage> TenantGet(Guid tenantId, string url) =>
        HttpHelpers.SendAsync(HttpMethod.Get, url, body: null,
            ("X-Tenant-Id", tenantId.ToString()), ("X-Tenant-Role", "tenant"));

    private static Task<HttpResponseMessage> TenantPost(Guid tenantId, string url, object body) =>
        HttpHelpers.SendAsync(HttpMethod.Post, url, body,
            ("X-Tenant-Id", tenantId.ToString()), ("X-Tenant-Role", "tenant"));
}

internal sealed record BundleOrdersResult(Guid BatchId, IReadOnlyList<Guid> ShipmentIds, IReadOnlyList<Guid> ConsumedPendingOrderIds);
