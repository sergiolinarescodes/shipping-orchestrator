using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShippingOrchestrator.PrivateApi.Authentication;
using ShippingOrchestrator.PrivateApi.Endpoints;

namespace ShippingOrchestrator.PrivateApi;

/// <summary>
/// Wires the PrivateApi-specific concerns — staff auth scheme + ops/admin endpoints — in
/// one place so both <c>Program.cs</c> and the E2E composite host stay in step. Shared
/// registrations (Core / Application / connector modules / Wolverine) remain at the host
/// level for the same reasons described in <see cref="PublicApi.PublicApiPipeline"/>.
/// </summary>
public static class PrivateApiPipeline
{
    public const string StaffPolicy = "Staff";

    public static AuthenticationBuilder AddPrivateApiStaffAuth(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        if (environment.IsProduction())
            throw new InvalidOperationException(
                $"{nameof(TestStaffAuthHandler)} is a dev-only authentication scheme. " +
                "Production must wire a real JWT bearer (e.g. Cognito) before this host can boot.");

        var auth = services.AddAuthentication(TestStaffAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestStaffAuthHandler>(TestStaffAuthHandler.SchemeName, _ => { });
        services.AddAuthorizationBuilder()
            .AddPolicy(StaffPolicy, policy => policy
                .AddAuthenticationSchemes(TestStaffAuthHandler.SchemeName)
                .RequireAuthenticatedUser());
        return auth;
    }

    public static IEndpointRouteBuilder MapPrivateApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapAdminEndpoints();
        app.MapOnboardingEndpoints();
        app.MapSimulatorEndpoints();
        app.MapTenantInspectionEndpoints();
        app.MapIngestionFailuresOpsEndpoints();
        return app;
    }
}
