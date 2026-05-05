using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using ShippingOrchestrator.E2E.Tests.Infrastructure;

namespace ShippingOrchestrator.E2E.Tests;

/// <summary>
/// Belt and braces for the webhook → tenant routing path. Anonymous platform pushes carry
/// only a store identifier (Shopify shop domain, WC store URL); the orchestrator must resolve
/// that to exactly one tenant and never leak an order across the boundary. This is the
/// invariant that backs the whole multi-tenant model — a Shopify push for Acme NL must land
/// in Acme NL's pending list and nowhere else, even if a different tenant happens to be
/// signed in to the dashboard at that moment.
/// </summary>
[TestFixture]
public class WebhookTenantRoutingTests : E2ETestBase
{
    private const string ShopifyClientSecret = "test-secret"; // matches E2ECompositeHost configuration

    [Test]
    public async Task Shopify_webhook_routes_to_the_tenant_that_owns_the_shop_domain()
    {
        var tenantA = await TenantBootstrap.CreateTenantAsync("Tenant A");
        var tenantB = await TenantBootstrap.CreateTenantAsync("Tenant B");
        await TenantBootstrap.AssignPostNlAsync(tenantA);
        await TenantBootstrap.AssignPostNlAsync(tenantB);

        var shopA = $"shop-a-{Guid.NewGuid():N}".Substring(0, 24) + ".myshopify.com";
        var shopB = $"shop-b-{Guid.NewGuid():N}".Substring(0, 24) + ".myshopify.com";
        await TenantBootstrap.InstallShopifyAsync(tenantA, shopA);
        await TenantBootstrap.InstallShopifyAsync(tenantB, shopB);

        var orderA = await PostShopifyWebhook(shopA, BuildShopifyOrder(orderId: 7001, name: "#A1"));
        var orderB = await PostShopifyWebhook(shopB, BuildShopifyOrder(orderId: 7002, name: "#B1"));
        orderA.pendingOrderId.Should().NotBeEmpty();
        orderB.pendingOrderId.Should().NotBeEmpty();

        await WaitForPendingOrderAsync(tenantA, orderA.pendingOrderId);
        await WaitForPendingOrderAsync(tenantB, orderB.pendingOrderId);

        var aPending = await PendingFor(tenantA);
        var bPending = await PendingFor(tenantB);

        aPending.Should().Contain(orderA.pendingOrderId, "tenant A's order must be visible to tenant A");
        aPending.Should().NotContain(orderB.pendingOrderId, "tenant A must NOT see tenant B's order");
        bPending.Should().Contain(orderB.pendingOrderId, "tenant B's order must be visible to tenant B");
        bPending.Should().NotContain(orderA.pendingOrderId, "tenant B must NOT see tenant A's order");
    }

    [Test]
    public async Task WooCommerce_webhook_routes_to_the_tenant_that_owns_the_store_url()
    {
        var tenantA = await TenantBootstrap.CreateTenantAsync("WC Tenant A");
        var tenantB = await TenantBootstrap.CreateTenantAsync("WC Tenant B");
        await TenantBootstrap.AssignPostNlAsync(tenantA);
        await TenantBootstrap.AssignPostNlAsync(tenantB);

        var storeA = $"https://wc-a-{Guid.NewGuid():N}".Substring(0, 38) + ".example.test";
        var storeB = $"https://wc-b-{Guid.NewGuid():N}".Substring(0, 38) + ".example.test";
        await TenantBootstrap.InstallWooCommerceAsync(tenantA, storeA);
        await TenantBootstrap.InstallWooCommerceAsync(tenantB, storeB);

        var orderA = await PostWooCommerceWebhook(storeA, BuildWooOrder(orderId: 8001, number: "A8001"));
        var orderB = await PostWooCommerceWebhook(storeB, BuildWooOrder(orderId: 8002, number: "B8002"));

        await WaitForPendingOrderAsync(tenantA, orderA.pendingOrderId);
        await WaitForPendingOrderAsync(tenantB, orderB.pendingOrderId);

        var aPending = await PendingFor(tenantA);
        var bPending = await PendingFor(tenantB);

        aPending.Should().Contain(orderA.pendingOrderId);
        aPending.Should().NotContain(orderB.pendingOrderId);
        bPending.Should().Contain(orderB.pendingOrderId);
        bPending.Should().NotContain(orderA.pendingOrderId);
    }

    private static async Task<Guid[]> PendingFor(Guid tenantId)
    {
        var resp = await HttpHelpers.SendAsync(HttpMethod.Get, "/v1/dashboard/orders/pending?take=100", body: null,
            ("X-Tenant-Id", tenantId.ToString()), ("X-Tenant-Role", "tenant"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return rows.EnumerateArray().Select(o => o.GetProperty("id").GetGuid()).ToArray();
    }

    private static async Task<(Guid pendingOrderId, HttpStatusCode status)> PostShopifyWebhook(string shopDomain, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var hmac = ComputeShopifyHmac(bytes, ShopifyClientSecret);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/shopify")
        {
            Content = new ByteArrayContent(bytes),
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        request.Headers.Add("X-Shopify-Shop-Domain", shopDomain);
        request.Headers.Add("X-Shopify-Hmac-Sha256", hmac);
        request.Headers.Add("X-Shopify-Topic", "orders/create");

        var resp = await E2EFixture.Current.HttpClient.SendAsync(request);
        resp.StatusCode.Should().Be(HttpStatusCode.Accepted, await resp.Content.ReadAsStringAsync());
        var payload = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return (payload.GetProperty("pendingOrderId").GetGuid(), resp.StatusCode);
    }

    private static async Task<(Guid pendingOrderId, HttpStatusCode status)> PostWooCommerceWebhook(string storeUrl, string body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/woocommerce")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-WC-Webhook-Source", storeUrl);
        request.Headers.Add("X-WC-Webhook-Topic", "order.created");

        var resp = await E2EFixture.Current.HttpClient.SendAsync(request);
        resp.StatusCode.Should().Be(HttpStatusCode.Accepted, await resp.Content.ReadAsStringAsync());
        var payload = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return (payload.GetProperty("pendingOrderId").GetGuid(), resp.StatusCode);
    }

    private static string ComputeShopifyHmac(byte[] body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(hmac.ComputeHash(body));
    }

    private static string BuildShopifyOrder(long orderId, string name) => $$"""
    {
      "id": {{orderId}},
      "name": "{{name}}",
      "email": "buyer@example.test",
      "currency": "EUR",
      "shop": { "name": "Routing Test", "country_code": "NL" },
      "shipping_address": {
        "first_name": "Test",
        "last_name": "Buyer",
        "address1": "Damrak 70",
        "city": "Amsterdam",
        "province": null,
        "zip": "1012 LM",
        "country_code": "NL"
      },
      "line_items": [
        { "sku": "ITEM", "title": "Item", "quantity": 1, "grams": 500, "price": "9.99" }
      ]
    }
    """;

    private static string BuildWooOrder(long orderId, string number) => $$"""
    {
      "id": {{orderId}},
      "number": "{{number}}",
      "currency": "EUR",
      "billing": {
        "first_name": "Test",
        "last_name": "Buyer",
        "address_1": "Damrak 70",
        "city": "Amsterdam",
        "postcode": "1012 LM",
        "country": "NL",
        "email": "buyer@example.test",
        "phone": "+31201234567"
      },
      "shipping": {
        "first_name": "Test",
        "last_name": "Buyer",
        "address_1": "Damrak 70",
        "city": "Amsterdam",
        "postcode": "1012 LM",
        "country": "NL"
      },
      "line_items": [
        {
          "id": 1,
          "name": "Item",
          "sku": "ITEM",
          "quantity": 1,
          "price": "9.99",
          "meta_data": [{ "key": "_weight", "value": "0.500" }]
        }
      ]
    }
    """;
}
