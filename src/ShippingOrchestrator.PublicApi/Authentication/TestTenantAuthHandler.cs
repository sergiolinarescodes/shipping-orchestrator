using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.PublicApi.Authentication;

/// <summary>
/// Dev/test authentication scheme: trusts the <c>X-Tenant-Id</c> header but verifies the
/// id resolves to a real <c>orchestrator.tenants</c> row before authenticating. Production
/// hosts wire Cognito JWT bearer instead and never enable this handler — but even in dev
/// we want a closer-to-prod contract: a token claim referencing a non-existent tenant must
/// fail closed. Otherwise the SPA's localStorage retains a stale tenant id across a
/// `dev-down -Clean` and the orchestrator silently accepts requests against a phantom
/// tenant, creating orphan connections and batches that the operator console can't render.
/// </summary>
public sealed class TestTenantAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestTenantHeader";

    public TestTenantAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Tenant-Id", out var values) || values.Count == 0)
            return AuthenticateResult.NoResult();

        var raw = values.ToString();
        if (!Guid.TryParse(raw, out var tenantId))
            return AuthenticateResult.Fail($"X-Tenant-Id is not a valid GUID: {raw}");

        var tenants = Context.RequestServices.GetRequiredService<ITenantRepository>();
        var tenant = await tenants.FindAsync(new TenantId(tenantId), Context.RequestAborted)
            .ConfigureAwait(false);
        if (tenant is null)
        {
            Logger.LogWarning("Rejected request with X-Tenant-Id {TenantId}: no tenant row exists.", tenantId);
            return AuthenticateResult.Fail($"X-Tenant-Id {tenantId} does not match any registered tenant.");
        }

        var role = Request.Headers.TryGetValue("X-Tenant-Role", out var roleValues)
            ? roleValues.ToString()
            : "tenant";

        var claims = new[]
        {
            new Claim("tenant_id", tenantId.ToString()),
            new Claim(ClaimTypes.Role, role),
            new Claim(ClaimTypes.NameIdentifier, tenantId.ToString()),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}
