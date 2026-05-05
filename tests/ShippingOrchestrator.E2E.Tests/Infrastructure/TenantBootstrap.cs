using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ShippingOrchestrator.PrivateApi.Endpoints;

namespace ShippingOrchestrator.E2E.Tests.Infrastructure;

/// <summary>
/// Shared bootstrap helper for E2E tests after the onboarding split: ops only creates a
/// tenant; every connector install is driven by the tenant from the dashboard. Replaces the
/// old "drive the 5-step wizard" pattern that doubled as test setup. Each helper here runs a
/// single, narrow side effect — call only what your test actually needs.
/// </summary>
internal static class TenantBootstrap
{
    public static async Task<Guid> CreateTenantAsync(string displayName, string? contactEmail = "ops@acme.test")
    {
        var resp = await HttpHelpers.SendAsync(HttpMethod.Post, "/admin/tenants",
            new { displayName, contactEmail },
            ("X-Staff-Role", "admin"), ("X-Staff-User", "ops-tester"));
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        var body = await resp.Content.ReadFromJsonAsync<CreateTenantResponse>();
        body.Should().NotBeNull();
        return body!.TenantId;
    }

    public static async Task AssignPostNlAsync(Guid tenantId)
    {
        var resp = await HttpHelpers.SendAsync(HttpMethod.Post, $"/admin/tenants/{tenantId}/carrier-assignments",
            new
            {
                carrierCode = "postnl",
                priority = 100,
                originCountries = new[] { "NL" },
                destinationCountries = new[] { "*" },
            },
            ("X-Staff-Role", "admin"), ("X-Staff-User", "ops-tester"));
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Drives the dashboard-flow Shopify install: tenant calls POST start, we extract the
    /// signed state from the authorize URL and round-trip the dashboard OAuth callback. With
    /// the InMemory connector this round-trip succeeds with a synthetic token; with the Real
    /// connector + WireMock the same path exchanges a stubbed code for an access token.
    /// </summary>
    public static async Task InstallShopifyAsync(Guid tenantId, string shopDomain)
    {
        var startResp = await HttpHelpers.SendAsync(HttpMethod.Post,
            "/v1/dashboard/connections/shopify/start",
            new { externalAccountId = shopDomain },
            ("X-Tenant-Id", tenantId.ToString()), ("X-Tenant-Role", "tenant"));
        startResp.StatusCode.Should().Be(HttpStatusCode.OK, await startResp.Content.ReadAsStringAsync());
        var startPayload = await startResp.Content.ReadFromJsonAsync<JsonElement>();
        var authUrl = startPayload.GetProperty("authorizationUrl").GetString()!;
        var state = ExtractQueryParam(authUrl, "state");
        state.Should().NotBeNullOrEmpty();

        var callbackResp = await E2EFixture.Current.HttpClient.GetAsync(
            $"/v1/connections/dashboard-callback/shopify?code=test-auth-code&state={Uri.EscapeDataString(state!)}&shop={Uri.EscapeDataString(shopDomain)}");
        callbackResp.IsSuccessStatusCode.Should().BeTrue(
            $"Shopify dashboard callback should succeed (status {callbackResp.StatusCode}, body: {await callbackResp.Content.ReadAsStringAsync()}).");
    }

    public static async Task<(Guid ConnectionId, string UserIdToken)> InstallWooCommerceAsync(Guid tenantId, string storeUrl)
    {
        var startResp = await HttpHelpers.SendAsync(HttpMethod.Post,
            "/v1/dashboard/connections/woocommerce/start",
            new { externalAccountId = storeUrl },
            ("X-Tenant-Id", tenantId.ToString()), ("X-Tenant-Role", "tenant"));
        startResp.StatusCode.Should().Be(HttpStatusCode.OK, await startResp.Content.ReadAsStringAsync());
        var startPayload = await startResp.Content.ReadFromJsonAsync<JsonElement>();
        var authUrl = startPayload.GetProperty("authorizationUrl").GetString()!;
        var userId = ExtractQueryParam(authUrl, "user_id")!;

        var callbackBody = new
        {
            key_id = 1,
            user_id = userId,
            consumer_key = $"ck_e2e_{Guid.NewGuid():N}",
            consumer_secret = $"cs_e2e_{Guid.NewGuid():N}",
            key_permissions = "read_write",
        };
        var callbackResp = await E2EFixture.Current.HttpClient.PostAsJsonAsync(
            "/v1/connections/dashboard-callback/woocommerce", callbackBody);
        callbackResp.StatusCode.Should().Be(HttpStatusCode.OK, await callbackResp.Content.ReadAsStringAsync());
        var connectionId = (await callbackResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("connectionId").GetGuid();
        return (connectionId, userId);
    }

    private static string? ExtractQueryParam(string url, string name)
    {
        var uri = new Uri(url, UriKind.Absolute);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        return query[name];
    }
}
