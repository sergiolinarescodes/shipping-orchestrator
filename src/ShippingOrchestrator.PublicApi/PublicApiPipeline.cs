using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShippingOrchestrator.PublicApi.Authentication;
using ShippingOrchestrator.PublicApi.Endpoints;

namespace ShippingOrchestrator.PublicApi;

/// <summary>
/// Wires the PublicApi-specific concerns — auth schemes + tenant-facing endpoints — in one
/// place so both <c>Program.cs</c> and the E2E composite host stay in step. Shared
/// registrations (<c>AddOrchestratorCore</c>, <c>AddOrchestratorApplication</c>, connector
/// modules, Wolverine) remain at the host level: they're stable, the same call across all
/// three hosts, and re-registering them via <c>TryAdd</c> in two places is fragile.
///
/// Auth model: the production scheme is <see cref="SessionAuthHandler"/> (HTTP-only cookie
/// backed by <c>orchestrator.auth_sessions</c>). In non-Production environments the legacy
/// <see cref="TestTenantAuthHandler"/> is also registered as a fallback so existing E2E tests
/// that assert behaviour via the <c>X-Tenant-Id</c> header continue to function. The default
/// scheme is always cookie; the test scheme is opt-in via the test fixture's
/// <c>[Authorize(AuthenticationSchemes = "TestTenantHeader")]</c> override.
/// </summary>
public static class PublicApiPipeline
{
    public const string TenantPolicy = "Tenant";
    public const string AccountPolicy = "Account";

    public static AuthenticationBuilder AddPublicApiAuth(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        var auth = services
            .AddAuthentication(SessionAuthHandler.SchemeName)
            .AddScheme<SessionAuthOptions, SessionAuthHandler>(SessionAuthHandler.SchemeName, _ => { });

        if (!environment.IsProduction())
        {
            auth.AddScheme<AuthenticationSchemeOptions, TestTenantAuthHandler>(
                TestTenantAuthHandler.SchemeName, _ => { });
        }

        // AccountPolicy gates account-scoped surfaces (auth/me, select-tenant, sign-out) and
        // requires the cookie scheme — the legacy header scheme cannot satisfy it.
        // TenantPolicy gates dashboard surfaces and accepts EITHER scheme as long as a
        // tenant_id claim is present, so existing E2E tests using X-Tenant-Id keep working.
        services.AddAuthorizationBuilder()
            .AddPolicy(AccountPolicy, policy => policy
                .AddAuthenticationSchemes(SessionAuthHandler.SchemeName)
                .RequireAuthenticatedUser()
                .RequireClaim(SessionAuthHandler.AccountIdClaim))
            .AddPolicy(TenantPolicy, policy => policy
                .AddAuthenticationSchemes(BuildAuthSchemes(environment))
                .RequireAuthenticatedUser()
                .RequireClaim(SessionAuthHandler.TenantIdClaim));

        return auth;
    }

    private static string[] BuildAuthSchemes(IHostEnvironment environment) =>
        environment.IsProduction()
            ? [SessionAuthHandler.SchemeName]
            : [SessionAuthHandler.SchemeName, TestTenantAuthHandler.SchemeName];

    public static IEndpointRouteBuilder MapPublicApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapAuthEndpoints();
        app.MapShipmentEndpoints();
        app.MapConnectionEndpoints();
        app.MapDashboardEndpoints();
        app.MapDashboardConnectionsEndpoints();
        app.MapDashboardConnectionCallbackEndpoints();
        app.MapPendingOrderEndpoints();
        app.MapNeedsAttentionEndpoints();
        app.MapWebhookEndpoints();
        return app;
    }
}
