using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using ShippingOrchestrator.E2E.Tests.Infrastructure;
using ShippingOrchestrator.ReadModels.Abstractions.Customer;

namespace ShippingOrchestrator.E2E.Tests;

/// <summary>
/// Drives the WooCommerce path end-to-end via the dashboard install flow:
/// tenant created via the existing onboarding wizard, then `POST /v1/dashboard/connections/
/// woocommerce/start` -> synthetic install URL -> `POST /v1/connections/dashboard-callback/
/// woocommerce` with the round-tripped user_id token + fake consumer creds. With the
/// connection persisted, post a WC `order.created` body to /v1/webhooks/woocommerce; the
/// composite host runs the WC connector in InMemory mode, so HMAC is skipped and the
/// translator + ingestion + bundle path is exercised. (HMAC is covered in
/// WooCommerceWebhookSignatureTests.)
/// </summary>
[TestFixture]
public class HappyPathWooCommerceWebhookTests : E2ETestBase
{
    private const string StoreUrl = "https://wc-happy.example.test";

    [Test]
    public async Task Dashboard_install_then_webhook_creates_pending_then_labeled_shipment()
    {
        var tenantId = await TenantBootstrap.CreateTenantAsync("Cross-Platform NL");
        await TenantBootstrap.AssignPostNlAsync(tenantId);
        await TenantBootstrap.InstallShopifyAsync(tenantId, $"wc-tenant-{Guid.NewGuid():N}".Substring(0, 24) + ".myshopify.com");

        var (connectionId, _) = await TenantBootstrap.InstallWooCommerceAsync(tenantId, StoreUrl);
        connectionId.Should().NotBeEmpty();

        var orderJson = BuildWooOrderBody(orderId: 9001, number: "9001");
        var webhookResp = await PostWooCommerceWebhook(StoreUrl, orderJson);
        webhookResp.StatusCode.Should().Be(HttpStatusCode.Accepted, await webhookResp.Content.ReadAsStringAsync());
        var webhookPayload = await webhookResp.Content.ReadFromJsonAsync<JsonElement>();
        var pendingOrderId = webhookPayload.GetProperty("pendingOrderId").GetGuid();
        pendingOrderId.Should().NotBeEmpty();

        await WaitForPendingOrderAsync(tenantId, pendingOrderId);

        var bundleResp = await TenantPost(tenantId, "/v1/dashboard/orders/bundle",
            new { orderIds = new[] { pendingOrderId } });
        bundleResp.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var bundle = await bundleResp.Content.ReadFromJsonAsync<BundleOrdersResult>();
        bundle.Should().NotBeNull();

        var completion = await E2EFixture.Current.BatchSignal.WaitAsync(bundle!.BatchId, TimeSpan.FromSeconds(30));
        completion.SuccessCount.Should().Be(1);

        var shipmentId = bundle.ShipmentIds[0];
        var customerView = await PollUntilLabeled(tenantId, shipmentId);
        customerView.CarrierCode.Should().Be("postnl");
        customerView.TrackingNumber.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task Realistic_woocommerce_body_without_weight_meta_records_zero_weight_failure()
    {
        // Regression for the original bug: WC's REST order shape doesn't carry per-line weight
        // (it lives on the product, not the order). The translator+webhook+post-enrich pipeline
        // must surface this as a typed ZeroWeight failure so the dashboard's "Needs attention"
        // panel can prompt the merchant to set the product weight + Recheck. The connector
        // runs in InMemory mode here, so the enricher is not implemented and the post-validate
        // step is the only thing protecting us from silently shipping zero-gram orders.
        var tenantId = await TenantBootstrap.CreateTenantAsync("Realistic WC NL");
        await TenantBootstrap.AssignPostNlAsync(tenantId);
        await TenantBootstrap.InstallWooCommerceAsync(tenantId, "https://wc-realistic.example.test");

        var orderJson = BuildRealisticWooOrderBody(orderId: 9100);
        var resp = await PostWooCommerceWebhook("https://wc-realistic.example.test", orderJson);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "ZeroWeight is a recorded failure (200 + reason in body) — NOT a 4xx. WP would otherwise mark our delivery URL as broken after 5 strikes.");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("recorded_failure");
        body.GetProperty("reasonCode").GetString().Should().Be("ZeroWeight");
        body.GetProperty("hint").GetString().Should().Contain("weight",
            "the customer-facing hint must mention what to fix");
    }

    private static string BuildRealisticWooOrderBody(long orderId) => $$"""
    {
      "id": {{orderId}},
      "number": "{{orderId}}",
      "currency": "EUR",
      "billing": {
        "first_name": "Eline", "last_name": "de Boer",
        "address_1": "Damrak 70", "city": "Amsterdam", "postcode": "1012 LM", "country": "NL",
        "email": "buyer@example.com", "phone": "+31201234567"
      },
      "shipping": {
        "first_name": "Eline", "last_name": "de Boer",
        "address_1": "Damrak 70", "city": "Amsterdam", "postcode": "1012 LM", "country": "NL"
      },
      "line_items": [
        { "id": 1, "name": "Acme widget", "sku": "ACME-WIDGET",
          "product_id": 42, "variation_id": 0, "quantity": 1, "price": "24.99",
          "meta_data": [] }
      ]
    }
    """;

    [Test]
    public async Task Disconnect_hard_deletes_the_connection_row()
    {
        var tenantId = await TenantBootstrap.CreateTenantAsync("Round-trip NL");
        var (connectionId, _) = await TenantBootstrap.InstallWooCommerceAsync(tenantId, "https://wc-roundtrip.example.test");

        // Sanity: the connection is present before disconnect.
        var listBefore = await TenantGet(tenantId, "/v1/dashboard/connections");
        var rowsBefore = (await listBefore.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("connections").EnumerateArray()
            .Select(c => c.GetProperty("connectionId").GetGuid()).ToArray();
        rowsBefore.Should().Contain(connectionId);

        var disconnectResp = await TenantPost(tenantId, $"/v1/dashboard/connections/{connectionId}/disconnect",
            new { reason = "tenant requested" });
        disconnectResp.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "disconnect is a hard delete — there's no row left to return a status for");

        // Connection no longer appears in the tenant's list. The product UX is "remove and
        // re-install fresh"; a follow-up Connect goes through the full OAuth + webhook
        // registration path so no stale credentials or platform-side hooks survive.
        var listAfter = await TenantGet(tenantId, "/v1/dashboard/connections");
        var rowsAfter = (await listAfter.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("connections").EnumerateArray()
            .Select(c => c.GetProperty("connectionId").GetGuid()).ToArray();
        rowsAfter.Should().NotContain(connectionId);

        // Idempotency: a second disconnect of the now-deleted id is a 404 (the row is gone).
        var second = await TenantPost(tenantId, $"/v1/dashboard/connections/{connectionId}/disconnect",
            new { reason = "double-tap" });
        second.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Disconnect_rejects_cross_tenant_id_with_403()
    {
        var ownerTenantId = await TenantBootstrap.CreateTenantAsync("Owner NL");
        var (connectionId, _) = await TenantBootstrap.InstallWooCommerceAsync(ownerTenantId, "https://wc-cross-tenant.example.test");

        // A second authenticated tenant (TestTenantAuthHandler 401s unknown tenant ids, so we
        // need an authenticated-but-unrelated principal to exercise the 403 path) tries to
        // mutate the first tenant's connection. The handler must verify connection.TenantId
        // matches the requesting tenant id and reject with Forbid.
        var attackerTenantId = await TenantBootstrap.CreateTenantAsync("Attacker NL");
        attackerTenantId.Should().NotBe(ownerTenantId);

        var resp = await TenantPost(attackerTenantId, $"/v1/dashboard/connections/{connectionId}/disconnect",
            new { reason = "spoof" });
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    internal static async Task<HttpResponseMessage> PostWooCommerceWebhook(string storeUrl, string body)
    {
        // InMemory mode: HMAC validation is skipped, so we only need the source header.
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/woocommerce")
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-WC-Webhook-Source", storeUrl);
        request.Headers.Add("X-WC-Webhook-Topic", "order.created");
        return await E2EFixture.Current.HttpClient.SendAsync(request);
    }

    internal static string BuildWooOrderBody(long orderId, string number) => $$"""
    {
      "id": {{orderId}},
      "number": "{{number}}",
      "currency": "EUR",
      "billing": {
        "first_name": "Eline",
        "last_name": "de Boer",
        "address_1": "Damrak 70",
        "city": "Amsterdam",
        "postcode": "1012 LM",
        "country": "NL",
        "email": "buyer@example.com",
        "phone": "+31201234567"
      },
      "shipping": {
        "first_name": "Eline",
        "last_name": "de Boer",
        "address_1": "Damrak 70",
        "city": "Amsterdam",
        "postcode": "1012 LM",
        "country": "NL"
      },
      "line_items": [
        {
          "id": 1,
          "name": "Acme widget",
          "sku": "ACME-WIDGET",
          "quantity": 1,
          "price": "24.99",
          "meta_data": [{ "key": "_weight", "value": "0.800" }]
        }
      ]
    }
    """;

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

    private static Task<HttpResponseMessage> TenantPost(Guid tenantId, string url, object? body) =>
        HttpHelpers.SendAsync(HttpMethod.Post, url, body,
            ("X-Tenant-Id", tenantId.ToString()), ("X-Tenant-Role", "tenant"));
}
