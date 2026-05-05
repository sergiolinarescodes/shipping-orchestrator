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
/// Drives the production Shopify path end-to-end: WireMock serves the OAuth token-exchange,
/// the wizard's redirect URI is hit on the anonymous PublicApi callback (not the dev-only
/// simulate-shopify-callback shortcut), and a Shopify-shaped order JSON is delivered to
/// /v1/webhooks/shopify with a real HMAC-SHA256 signature. Validates the orchestrator
/// pipeline behind a real-shape Shopify integration without requiring a Remix tunnel or
/// public callback URL.
/// </summary>
[TestFixture]
public class HappyPathShopifyWebhookTests : E2ETestBase
{
    private const string ShopDomain = "acme-webhook.myshopify.com";
    private const string ShopifyClientSecret = "test-secret"; // matches E2ECompositeHost configuration

    [Test]
    public async Task Webhook_with_valid_HMAC_creates_pending_order_then_labeled_shipment()
    {
        var tenantId = await DriveOnboardingThroughRealOAuthCallback();

        var orderJson = BuildShopifyOrderBody(orderId: 1099001L, name: "#1001");
        var webhookResp = await PostShopifyWebhook(orderJson);
        webhookResp.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var webhookPayload = await webhookResp.Content.ReadFromJsonAsync<JsonElement>();
        var pendingOrderId = webhookPayload.GetProperty("pendingOrderId").GetGuid();
        pendingOrderId.Should().NotBeEmpty();

        // Webhook is fire-and-forget — wait for the in-flight Wolverine handler to commit
        // before bundling, otherwise the bundle endpoint hits an empty pending row.
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
        customerView.LabelUri.Should().StartWith("https://mock.postnl.local/labels/");
    }

    [Test]
    public async Task Webhook_with_invalid_HMAC_returns_unauthorized()
    {
        // HMAC validation runs before the connection lookup, so no onboarding is required.
        var bytes = Encoding.UTF8.GetBytes(BuildShopifyOrderBody(orderId: 1099002L, name: "#1002"));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/shopify")
        {
            Content = new ByteArrayContent(bytes),
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        request.Headers.Add("X-Shopify-Shop-Domain", ShopDomain);
        request.Headers.Add("X-Shopify-Hmac-Sha256", "deliberatelywrongbase64==");
        request.Headers.Add("X-Shopify-Topic", "orders/create");

        var resp = await E2EFixture.Current.HttpClient.SendAsync(request);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Tenant-only ops handoff + tenant-driven Shopify install via the dashboard flow. The
    /// Real-mode connector's CompleteOAuthAsync runs against WireMock (token exchange) and
    /// persists an EcommerceConnection keyed on <see cref="ShopDomain"/>.
    /// </summary>
    private static async Task<Guid> DriveOnboardingThroughRealOAuthCallback()
    {
        var tenantId = await TenantBootstrap.CreateTenantAsync("Acme Webhook NL");
        await TenantBootstrap.AssignPostNlAsync(tenantId);
        await TenantBootstrap.InstallShopifyAsync(tenantId, ShopDomain);
        return tenantId;
    }

    private static async Task<HttpResponseMessage> PostShopifyWebhook(string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var hmac = ComputeShopifyHmac(bytes, ShopifyClientSecret);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/shopify")
        {
            Content = new ByteArrayContent(bytes),
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        request.Headers.Add("X-Shopify-Shop-Domain", ShopDomain);
        request.Headers.Add("X-Shopify-Hmac-Sha256", hmac);
        request.Headers.Add("X-Shopify-Topic", "orders/create");
        request.Headers.Add("X-Shopify-API-Version", "2024-10");

        return await E2EFixture.Current.HttpClient.SendAsync(request);
    }

    private static string ComputeShopifyHmac(byte[] body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(hmac.ComputeHash(body));
    }

    /// <summary>
    /// Hand-crafted JSON matching the subset of Shopify's <c>orders/create</c> body that
    /// <see cref="ShippingOrchestrator.EcommerceConnectors.Shopify.ShopifyOrderTranslator"/>
    /// reads. Snake-case keys because the translator's deserializer uses
    /// <see cref="JsonNamingPolicy.SnakeCaseLower"/>.
    /// </summary>
    private static string BuildShopifyOrderBody(long orderId, string name) => $$"""
    {
      "id": {{orderId}},
      "name": "{{name}}",
      "email": "buyer@acme.test",
      "currency": "EUR",
      "shop": { "name": "Acme Webhook NL", "country_code": "NL" },
      "shipping_address": {
        "first_name": "Jamie",
        "last_name": "Buyer",
        "address1": "Damrak 70",
        "address2": null,
        "city": "Amsterdam",
        "province": "Noord-Holland",
        "zip": "1012 LM",
        "country_code": "NL",
        "phone": "+31201234567"
      },
      "line_items": [
        {
          "sku": "ACME-WIDGET",
          "title": "Acme widget",
          "quantity": 1,
          "grams": 800,
          "price": "24.99"
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

    private static Task<HttpResponseMessage> TenantPost(Guid tenantId, string url, object body) =>
        HttpHelpers.SendAsync(HttpMethod.Post, url, body,
            ("X-Tenant-Id", tenantId.ToString()), ("X-Tenant-Role", "tenant"));
}
