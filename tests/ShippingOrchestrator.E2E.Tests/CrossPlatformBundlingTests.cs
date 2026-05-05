using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using ShippingOrchestrator.E2E.Tests.Infrastructure;
using ShippingOrchestrator.ReadModels.Abstractions.Customer;

namespace ShippingOrchestrator.E2E.Tests;

/// <summary>
/// The load-bearing demo proof: one tenant has both a Shopify connection and a WooCommerce
/// connection (the same TenantId, two distinct EcommerceConnection rows differing only by
/// platform_code). An order placed in either store lands in the same pending inbox; bundling
/// a Shopify row + a WC row in the same call produces one batch with two shipments and the
/// orchestrator pipeline never branches on platform.
/// </summary>
[TestFixture]
public class CrossPlatformBundlingTests : E2ETestBase
{
    private const string ShopDomain = "cross-tenant.myshopify.com";
    private const string ShopifyClientSecret = "test-secret"; // matches E2ECompositeHost
    private const string WooStoreUrl = "https://cross-tenant.example.test";

    [Test]
    public async Task Same_tenant_with_two_platforms_bundles_orders_into_one_batch()
    {
        var tenantId = await TenantBootstrap.CreateTenantAsync("Cross-Platform NL");
        await TenantBootstrap.AssignPostNlAsync(tenantId);
        await TenantBootstrap.InstallShopifyAsync(tenantId, ShopDomain);

        // Wire WooCommerce on top — same tenant, second EcommerceConnection row.
        var (wcConnectionId, _) = await TenantBootstrap.InstallWooCommerceAsync(tenantId, WooStoreUrl);
        wcConnectionId.Should().NotBeEmpty();

        // Verify the dashboard sees both as one tenant's connections.
        var listResp = await TenantGet(tenantId, "/v1/dashboard/connections");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var conns = list.GetProperty("connections");
        conns.GetArrayLength().Should().Be(2);
        var platforms = conns.EnumerateArray().Select(c => c.GetProperty("platformCode").GetString()).ToHashSet();
        platforms.Should().BeEquivalentTo(new[] { "shopify", "woocommerce" });

        // Post one Shopify order (Real-mode HMAC) + one WooCommerce order (InMemory, no HMAC).
        var shopifyPending = await PostShopifyOrderAndGetPendingId(orderId: 5101, name: "#5101");
        var wooPending = await PostWooCommerceOrderAndGetPendingId(orderId: 5102, number: "5102");

        // Webhooks ack before the in-flight ingest handler commits, so wait for both rows
        // to land before bundling — otherwise the bundle command sees an empty inbox.
        await WaitForPendingOrderAsync(tenantId, shopifyPending);
        await WaitForPendingOrderAsync(tenantId, wooPending);

        // Bundle both pending orders together — single command, one batch, two shipments.
        var bundleResp = await TenantPost(tenantId, "/v1/dashboard/orders/bundle",
            new { orderIds = new[] { shopifyPending, wooPending } });
        bundleResp.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var bundle = await bundleResp.Content.ReadFromJsonAsync<BundleResult>();
        bundle.Should().NotBeNull();
        bundle!.ShipmentIds.Should().HaveCount(2);
        bundle.ConsumedPendingOrderIds.Should().BeEquivalentTo(new[] { shopifyPending, wooPending });

        var completion = await E2EFixture.Current.BatchSignal.WaitAsync(bundle.BatchId, TimeSpan.FromSeconds(30));
        completion.SuccessCount.Should().Be(2);

        // Both shipments end up labeled by the same carrier connector.
        foreach (var shipmentId in bundle.ShipmentIds)
        {
            var view = await PollUntilLabeled(tenantId, shipmentId);
            view.CarrierCode.Should().Be("postnl");
            view.TrackingNumber.Should().NotBeNullOrEmpty();
        }
    }

    private static async Task<Guid> PostShopifyOrderAndGetPendingId(long orderId, string name)
    {
        var body = $$"""
        {
          "id": {{orderId}},
          "name": "{{name}}",
          "email": "buyer@acme.test",
          "currency": "EUR",
          "shop": { "name": "Cross-Platform NL", "country_code": "NL" },
          "shipping_address": {
            "first_name": "Pim",
            "last_name": "Visser",
            "address1": "Damrak 70",
            "city": "Amsterdam",
            "province": "Noord-Holland",
            "zip": "1012 LM",
            "country_code": "NL"
          },
          "line_items": [{ "sku": "SHP-1", "title": "Shopify item", "quantity": 1, "grams": 600, "price": "19.99" }]
        }
        """;
        var bytes = Encoding.UTF8.GetBytes(body);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(ShopifyClientSecret));
        var sig = Convert.ToBase64String(hmac.ComputeHash(bytes));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/shopify")
        {
            Content = new ByteArrayContent(bytes),
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        request.Headers.Add("X-Shopify-Shop-Domain", ShopDomain);
        request.Headers.Add("X-Shopify-Hmac-Sha256", sig);
        request.Headers.Add("X-Shopify-Topic", "orders/create");

        var resp = await E2EFixture.Current.HttpClient.SendAsync(request);
        resp.StatusCode.Should().Be(HttpStatusCode.Accepted, await resp.Content.ReadAsStringAsync());
        var payload = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("pendingOrderId").GetGuid();
    }

    private static async Task<Guid> PostWooCommerceOrderAndGetPendingId(long orderId, string number)
    {
        var body = HappyPathWooCommerceWebhookTests.BuildWooOrderBody(orderId, number);
        var resp = await HappyPathWooCommerceWebhookTests.PostWooCommerceWebhook(WooStoreUrl, body);
        resp.StatusCode.Should().Be(HttpStatusCode.Accepted, await resp.Content.ReadAsStringAsync());
        var payload = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("pendingOrderId").GetGuid();
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

    private static Task<HttpResponseMessage> TenantGet(Guid tenantId, string url) =>
        HttpHelpers.SendAsync(HttpMethod.Get, url, body: null,
            ("X-Tenant-Id", tenantId.ToString()), ("X-Tenant-Role", "tenant"));

    private static Task<HttpResponseMessage> TenantPost(Guid tenantId, string url, object body) =>
        HttpHelpers.SendAsync(HttpMethod.Post, url, body,
            ("X-Tenant-Id", tenantId.ToString()), ("X-Tenant-Role", "tenant"));

    private sealed record BundleResult(Guid BatchId, IReadOnlyList<Guid> ShipmentIds, IReadOnlyList<Guid> ConsumedPendingOrderIds);
}
