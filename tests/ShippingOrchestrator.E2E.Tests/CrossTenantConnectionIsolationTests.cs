using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using ShippingOrchestrator.E2E.Tests.Infrastructure;

namespace ShippingOrchestrator.E2E.Tests;

/// <summary>
/// Walls off every tenant-scoped connection mutation so a malicious or buggy caller can't
/// touch another tenant's store. Each assertion targets one of the routes the customer SPA
/// calls — list, disconnect — and the callback path that completes a fresh install. The
/// disconnect case has been around for a while; the rest were added after a real bug where
/// two tenants could legitimately register the same WC store URL and the inbound webhook
/// would resolve to the wrong row, leaking deliveries across the boundary.
/// </summary>
[TestFixture]
public class CrossTenantConnectionIsolationTests : E2ETestBase
{
    [Test]
    public async Task List_returns_only_the_callers_own_connections()
    {
        var ownerTenantId = await TenantBootstrap.CreateTenantAsync("Owner");
        var attackerTenantId = await TenantBootstrap.CreateTenantAsync("Attacker");
        var (ownerConnId, _) = await TenantBootstrap.InstallWooCommerceAsync(ownerTenantId, "https://owner-list.example.test");

        var attackerListResp = await TenantGet(attackerTenantId, "/v1/dashboard/connections");
        attackerListResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var attackerList = await attackerListResp.Content.ReadFromJsonAsync<JsonElement>();
        var attackerConns = attackerList.GetProperty("connections").EnumerateArray()
            .Select(c => c.GetProperty("connectionId").GetGuid()).ToArray();
        attackerConns.Should().NotContain(ownerConnId,
            "the dashboard connections list must scope to the calling tenant — leaking another tenant's connection ids would expose disconnect/reconnect targets");

        var ownerListResp = await TenantGet(ownerTenantId, "/v1/dashboard/connections");
        var ownerList = await ownerListResp.Content.ReadFromJsonAsync<JsonElement>();
        var ownerConns = ownerList.GetProperty("connections").EnumerateArray()
            .Select(c => c.GetProperty("connectionId").GetGuid()).ToArray();
        ownerConns.Should().Contain(ownerConnId);
    }

    [Test]
    public async Task Disconnect_rejects_cross_tenant_id_with_403()
    {
        var ownerTenantId = await TenantBootstrap.CreateTenantAsync("Owner");
        var (connectionId, _) = await TenantBootstrap.InstallWooCommerceAsync(ownerTenantId, "https://owner-disconnect-cross.example.test");

        var attackerTenantId = await TenantBootstrap.CreateTenantAsync("Attacker");
        var attempt = await TenantPost(attackerTenantId, $"/v1/dashboard/connections/{connectionId}/disconnect",
            new { reason = "spoof" });
        attempt.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "disconnect is the only mutation now (no reconnect endpoint). The handler must verify connection.TenantId before deleting.");
    }

    [Test]
    public async Task Install_start_endpoint_attaches_connection_to_calling_tenant_only()
    {
        var tenantA = await TenantBootstrap.CreateTenantAsync("Tenant A");
        var tenantB = await TenantBootstrap.CreateTenantAsync("Tenant B");

        // Both tenants legitimately install the same WC store URL. With the unique index
        // keyed on (tenant, platform, account), each should end up with its own row — the
        // start state is signed against the calling tenant id and the callback uses that id.
        // The webhook lookup tiebreak (most-recent-installed) is covered separately at the
        // repository layer.
        var sharedStore = "https://shared-store.example.test";
        var (connA, _) = await TenantBootstrap.InstallWooCommerceAsync(tenantA, sharedStore);
        var (connB, _) = await TenantBootstrap.InstallWooCommerceAsync(tenantB, sharedStore);
        connA.Should().NotBe(connB, "each tenant gets its own connection row even when the store URL is reused");

        var aList = (await (await TenantGet(tenantA, "/v1/dashboard/connections"))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("connections").EnumerateArray()
            .Select(c => c.GetProperty("connectionId").GetGuid()).ToArray();
        var bList = (await (await TenantGet(tenantB, "/v1/dashboard/connections"))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("connections").EnumerateArray()
            .Select(c => c.GetProperty("connectionId").GetGuid()).ToArray();

        aList.Should().Contain(connA).And.NotContain(connB);
        bList.Should().Contain(connB).And.NotContain(connA);
    }

    [Test]
    public async Task Tenant_with_unknown_id_in_header_is_rejected_with_401()
    {
        // TestTenantAuthHandler validates X-Tenant-Id against orchestrator.tenants and 401s
        // unknown ids (mirrors a real JWT with an unknown subject). This guard is what stops
        // a caller from minting an arbitrary header and writing under a phantom tenant.
        var phantom = Guid.NewGuid();
        var resp = await TenantGet(phantom, "/v1/dashboard/connections");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static Task<HttpResponseMessage> TenantGet(Guid tenantId, string url) =>
        HttpHelpers.SendAsync(HttpMethod.Get, url, body: null,
            ("X-Tenant-Id", tenantId.ToString()), ("X-Tenant-Role", "tenant"));

    private static Task<HttpResponseMessage> TenantPost(Guid tenantId, string url, object? body) =>
        HttpHelpers.SendAsync(HttpMethod.Post, url, body,
            ("X-Tenant-Id", tenantId.ToString()), ("X-Tenant-Role", "tenant"));
}
