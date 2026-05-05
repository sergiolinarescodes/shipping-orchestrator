using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using ShippingOrchestrator.E2E.Tests.Infrastructure;
using ShippingOrchestrator.PrivateApi.Endpoints;

namespace ShippingOrchestrator.E2E.Tests;

[TestFixture]
public class OnboardingFlowTests : E2ETestBase
{
    [Test]
    public async Task Manual_staff_flow_creates_active_tenant_and_returns_dashboard_url()
    {
        var startResp = await StaffPost("/admin/onboarding/", new StartOnboardingHttpRequest("manual-staff-v1", "ops@acme.test"));
        startResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var start = await startResp.Content.ReadFromJsonAsync<StartOnboardingResponse>();
        var processId = start!.ProcessId;

        var tenantPayload = new { displayName = "Acme NL", contactEmail = "ops@acme.test" };
        var step1 = await StaffPostJson($"/admin/onboarding/{processId}/steps/tenant.create.active/advance", tenantPayload);
        step1.StatusCode.Should().Be(HttpStatusCode.OK);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        JsonDocument? final = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            final = await GetProcess(processId);
            if (final.RootElement.GetProperty("status").GetString() == "Completed") break;
            await Task.Delay(100);
        }
        final.Should().NotBeNull();
        final!.RootElement.GetProperty("status").GetString().Should().Be("Completed");
        final.RootElement.GetProperty("tenantId").ValueKind.Should().NotBe(JsonValueKind.Null);
        final.RootElement.GetProperty("dashboardUrl").GetString().Should().Contain("/login?tenant=");
    }

    [Test]
    public async Task Validation_failure_marks_step_failed_and_allows_retry()
    {
        var startResp = await StaffPost("/admin/onboarding/", new StartOnboardingHttpRequest("manual-staff-v1", null));
        var start = await startResp.Content.ReadFromJsonAsync<StartOnboardingResponse>();
        var processId = start!.ProcessId;

        var bad = new { displayName = "", contactEmail = (string?)null };
        var failResp = await StaffPostJson($"/admin/onboarding/{processId}/steps/tenant.create.active/advance", bad);
        failResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var ok = new { displayName = "Acme retry", contactEmail = "ops@acme.test" };
        var okResp = await StaffPostJson($"/admin/onboarding/{processId}/steps/tenant.create.active/advance", ok);
        okResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Cancel_marks_process_cancelled()
    {
        var startResp = await StaffPost("/admin/onboarding/", new StartOnboardingHttpRequest("manual-staff-v1", null));
        var start = await startResp.Content.ReadFromJsonAsync<StartOnboardingResponse>();
        var processId = start!.ProcessId;

        var cancelResp = await StaffPost($"/admin/onboarding/{processId}/cancel", new { reason = "abandoned" });
        cancelResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var view = await GetProcess(processId);
        view.RootElement.GetProperty("status").GetString().Should().Be("Cancelled");
    }

    [Test]
    public async Task Direct_admin_tenant_create_returns_dashboard_url()
    {
        var resp = await StaffPost("/admin/tenants",
            new { displayName = "Direct Tenant", contactEmail = "ops@acme.test" });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("tenantId").GetGuid().Should().NotBeEmpty();
        body.GetProperty("status").GetString().Should().Be("Active");
        body.GetProperty("dashboardUrl").GetString().Should().Contain("/login?tenant=");
    }

    private static async Task<JsonDocument> GetProcess(Guid processId)
    {
        var resp = await StaffGet($"/admin/onboarding/{processId}");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body);
    }

    private static Task<HttpResponseMessage> StaffGet(string url) =>
        HttpHelpers.SendAsync(HttpMethod.Get, url, body: null,
            ("X-Staff-Role", "admin"), ("X-Staff-User", "ops-tester"));

    private static Task<HttpResponseMessage> StaffPost(string url, object? body) =>
        HttpHelpers.SendAsync(HttpMethod.Post, url, body,
            ("X-Staff-Role", "admin"), ("X-Staff-User", "ops-tester"));

    private static Task<HttpResponseMessage> StaffPostJson(string url, object body) =>
        HttpHelpers.SendAsync(HttpMethod.Post, url, body,
            ("X-Staff-Role", "admin"), ("X-Staff-User", "ops-tester"));
}
