using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShippingOrchestrator.Application;
using ShippingOrchestrator.CarrierConnectors.PostNL;
using ShippingOrchestrator.E2E.Tests.Wolverine;
using ShippingOrchestrator.EcommerceConnectors.Shopify;
using ShippingOrchestrator.EcommerceConnectors.WooCommerce;
using ShippingOrchestrator.Infrastructure;
using ShippingOrchestrator.Infrastructure.Persistence;
using ShippingOrchestrator.Infrastructure.Wolverine;
using ShippingOrchestrator.Modules.Abstractions;
using ShippingOrchestrator.PrivateApi;
using ShippingOrchestrator.PrivateApi.Authentication;
using ShippingOrchestrator.PublicApi;
using ShippingOrchestrator.PublicApi.Authentication;
using ShippingOrchestrator.PublicApi.Endpoints;
using ShippingOrchestrator.PublicApi.Realtime;
using ShippingOrchestrator.ReadModels;
using ShippingOrchestrator.ReadModels.Customer.Persistence;
using ShippingOrchestrator.ReadModels.Operations.Persistence;
using ShippingOrchestrator.ReadModels.Projections;
using ShippingOrchestrator.ReadModels.Realtime;
using Wolverine;

namespace ShippingOrchestrator.E2E.Tests.Infrastructure;

/// <summary>
/// One in-process host that bundles every endpoint (PublicApi + PrivateApi) and every
/// handler (Application + Worker + Projections) so the E2E suite can exercise the entire
/// flow. Messaging goes through SQS (LocalStack via Testcontainers) + the Postgres outbox —
/// the same wire shape used in production. In production these run as three separate
/// Fargate tasks; here they share one process but the message contracts and durability
/// guarantees are identical.
///
/// Endpoint mapping delegates to <see cref="PublicApiPipeline.MapPublicApiEndpoints"/> and
/// <see cref="PrivateApiPipeline.MapPrivateApiEndpoints"/> so adding a new endpoint to
/// either host's <c>Program.cs</c> automatically reaches the E2E suite. Authentication is
/// bespoke here (one process, two schemes) so we do NOT call the pipeline auth helpers —
/// composite registers both schemes side-by-side with policies that pin to their scheme.
/// </summary>
public static class E2ECompositeHost
{
    public static async Task<WebApplication> BuildAsync(string connectionString, int localStackPort, string shopifyAuthorityOverride, string? wooCommerceAuthorityOverride = null)
    {
        // Development env is required for two things: the dev-only auth handlers (the host
        // throws in Production) and Wolverine's UseLocalStackIfDevelopment which redirects
        // the SQS client to LocalStack instead of real AWS.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.WebHost.UseTestServer();

        builder.Configuration["ConnectionStrings:Orchestrator"] = connectionString;
        builder.Configuration["ConnectionStrings:CustomerRead"] = connectionString;
        builder.Configuration["ConnectionStrings:OperationsRead"] = connectionString;
        builder.Configuration["Encryption:Aes:Base64Key"] = Convert.ToBase64String(new byte[32]);
        builder.Configuration["Aws:LocalStackPort"] = localStackPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
        builder.Configuration["Messaging:AutoPurgeOnStartup"] = "true";
        // Real Shopify connector pointed at WireMock so OAuth token-exchange + HMAC validation
        // run their production code paths under test. The InMemory adapter is exercised by
        // the connector unit tests; here we want the wire-shape contract.
        builder.Configuration["Connectors:Shopify:Mode"] = nameof(ConnectorMode.Real);
        builder.Configuration["Connectors:Shopify:ClientId"] = "test-client";
        builder.Configuration["Connectors:Shopify:ClientSecret"] = "test-secret";
        builder.Configuration["Connectors:Shopify:AuthorityOverride"] = shopifyAuthorityOverride;
        // WooCommerce connector — Real mode lets translator + HMAC paths run; AuthorityOverride
        // routes the WC REST webhook-registration call to WireMock when one is provided. Defaults
        // to InMemory so existing fixtures that don't exercise WC keep working.
        if (wooCommerceAuthorityOverride is not null)
        {
            builder.Configuration["Connectors:WooCommerce:Mode"] = nameof(ConnectorMode.Real);
            builder.Configuration["Connectors:WooCommerce:AuthorityOverride"] = wooCommerceAuthorityOverride;
        }
        else
        {
            builder.Configuration["Connectors:WooCommerce:Mode"] = nameof(ConnectorMode.InMemory);
        }
        builder.Configuration["Connectors:WooCommerce:AppName"] = "Ship Shipping (E2E)";
        builder.Configuration["Connectors:WooCommerce:Scope"] = "read_write";
        builder.Configuration["Connectors:WooCommerce:CallbackBaseUrl"] = "http://localhost";
        builder.Configuration["Connectors:WooCommerce:OrchestratorWebhookUrl"] = "http://localhost/v1/webhooks/woocommerce";
        builder.Configuration["Connectors:PostNL:Mode"] = nameof(ConnectorMode.InMemory);
        builder.Configuration["Connectors:PostNL:InMemory:MinLatencyMs"] = "5";
        builder.Configuration["Connectors:PostNL:InMemory:MaxLatencyMs"] = "20";
        builder.Configuration["Connectors:PostNL:InMemory:FailureProbability"] = "0";
        builder.Configuration["Simulators:Enabled"] = "true";
        builder.Configuration["ShipmentTracking:IntervalSeconds"] = "1";
        builder.Configuration["ShipmentTracking:MaxAttempts"] = "3";

        builder.Services.AddOrchestratorCore(builder.Configuration);
        builder.Services.AddOrchestratorApplication();
        builder.Services.AddCustomerReadPlatform(builder.Configuration);
        builder.Services.AddOperationsReadPlatform(builder.Configuration);

        builder.Services.AddConnectorModule<ShopifyConnectorModule>(builder.Configuration);
        builder.Services.AddConnectorModule<WooCommerceConnectorModule>(builder.Configuration);
        builder.Services.AddConnectorModule<PostNlConnectorModule>(builder.Configuration);

        // Composite auth: both dev-only schemes side-by-side; policies pin to their scheme so
        // routing works regardless of which is the default.
        builder.Services.AddAuthentication(TestTenantAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestTenantAuthHandler>(TestTenantAuthHandler.SchemeName, _ => { })
            .AddScheme<AuthenticationSchemeOptions, TestStaffAuthHandler>(TestStaffAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(PublicApiPipeline.TenantPolicy, p => p
                .AddAuthenticationSchemes(TestTenantAuthHandler.SchemeName)
                .RequireAuthenticatedUser())
            .AddPolicy(PrivateApiPipeline.StaffPolicy, p => p
                .AddAuthenticationSchemes(TestStaffAuthHandler.SchemeName)
                .RequireAuthenticatedUser());

        builder.Services.AddSingleton<BatchCompletionSignal>();
        builder.Services.AddDataProtection();
        builder.Services.AddSingleton<InstallStateProtector>();

        // Mirror PublicApi/Program.cs — webhook endpoints depend on the rate limiter singleton.
        builder.Services.Configure<WebhookRateLimitOptions>(
            builder.Configuration.GetSection(WebhookRateLimitOptions.SectionName));
        builder.Services.AddSingleton<IRateLimiter, InMemoryRateLimiter>();

        // The webhook endpoint accepts an IRealtimeNotifier parameter. Without a DI
        // registration, ASP.NET's minimal-API binder falls back to treating it as a request
        // body, which fails JSON deserialization on every incoming POST. The hub itself isn't
        // exercised by the E2E suite, so a no-op SignalR setup is all we need here.
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();
        // Projection handlers (ShipmentProjectionHandler etc.) take an IDashboardBroadcaster.
        // Without it Wolverine fails to construct the handler and the batch lifecycle never
        // completes — tests that wait on BatchSignal time out. The E2E suite doesn't assert
        // on the broadcast itself, so a no-op is enough.
        builder.Services.AddSingleton<IDashboardBroadcaster, NoopDashboardBroadcaster>();

        builder.Host.UseWolverine(opts => opts.ConfigureOrchestratorMessaging(
            builder.Configuration,
            typeof(ShipmentProjectionHandler).Assembly,
            typeof(BatchCompletionSignal).Assembly));

        builder.Services.AddProblemDetails();

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>().Database.Migrate();
            scope.ServiceProvider.GetRequiredService<OperationsReadDbContext>().Database.Migrate();
            scope.ServiceProvider.GetRequiredService<CustomerReadDbContext>().Database.Migrate();
        }

        await app.Services.BootstrapConnectorModulesAsync().ConfigureAwait(false);
        app.Services.RegisterConnectorModules();

        app.UseAuthentication();
        app.UseMiddleware<TenantContextMiddleware>();
        app.UseAuthorization();

        app.MapPublicApiEndpoints();
        app.MapPrivateApiEndpoints();
        app.MapGet("/healthz", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

        return app;
    }

    private sealed class NoopDashboardBroadcaster : IDashboardBroadcaster
    {
        public Task BroadcastAsync(Guid tenantId, string eventName, string? area, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
