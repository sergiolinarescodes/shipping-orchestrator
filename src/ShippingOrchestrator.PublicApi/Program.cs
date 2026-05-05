using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using ShippingOrchestrator.Application;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.CarrierConnectors.PostNL;
using ShippingOrchestrator.EcommerceConnectors.Shopify;
using ShippingOrchestrator.EcommerceConnectors.WooCommerce;
using ShippingOrchestrator.Infrastructure;
using ShippingOrchestrator.Infrastructure.Persistence;
using ShippingOrchestrator.Infrastructure.Telemetry;
using ShippingOrchestrator.Infrastructure.Wolverine;
using ShippingOrchestrator.Modules.Abstractions;
using ShippingOrchestrator.PublicApi;
using ShippingOrchestrator.PublicApi.Authentication;
using ShippingOrchestrator.PublicApi.Endpoints;
using ShippingOrchestrator.PublicApi.Realtime;
using Microsoft.Extensions.Options;
using ShippingOrchestrator.ReadModels;
using ShippingOrchestrator.ReadModels.Customer.Persistence;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOrchestratorCore(builder.Configuration);
builder.Services.AddOrchestratorApplication();
// PublicApi only reads from the customer schema — opt into the replica when configured so the
// hot dashboard path drains a follower instead of the primary writer.
builder.Services.AddCustomerReadPlatform(builder.Configuration, preferReadReplica: true);
builder.Services.AddOrchestratorTelemetry(builder.Configuration, "shipping-orchestrator-public-api");

builder.Services.AddConnectorModule<ShopifyConnectorModule>(builder.Configuration);
builder.Services.AddConnectorModule<WooCommerceConnectorModule>(builder.Configuration);
builder.Services.AddConnectorModule<PostNlConnectorModule>(builder.Configuration);

builder.Services.AddPublicApiAuth(builder.Environment);
builder.Services.AddDataProtection();
builder.Services.AddSingleton<InstallStateProtector>();

builder.Services.Configure<WebhookRateLimitOptions>(
    builder.Configuration.GetSection(WebhookRateLimitOptions.SectionName));

// A single Redis connection string fans out to two DI consumers — the SignalR backplane
// (so an event published on pod A reaches a client on pod B) and the distributed webhook
// rate limiter (so the per-tenant cap is enforced globally instead of per-pod). When unset,
// both fall back to single-pod-safe in-memory behaviour.
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(
        StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnectionString));
    builder.Services.AddSingleton<IRateLimiter, RedisRateLimiter>();
}
else
{
    builder.Services.AddSingleton<IRateLimiter, InMemoryRateLimiter>();
}

builder.Host.UseWolverine(opts => opts.ConfigureOrchestratorMessaging(
    builder.Configuration,
    typeof(ShippingOrchestrator.PublicApi.Realtime.BroadcastDashboardHandler).Assembly));

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// SignalR hub at /v1/realtime lets the customer dashboard subscribe to
// "dashboard:invalidate" events instead of polling. When the Redis connection string above
// is set, SignalR uses StackExchange.Redis as a backplane so an event published on pod A
// reaches a client connected to pod B; otherwise the in-process backplane is used. The
// notifier is a singleton so any request scope can dispatch without re-resolving the hub
// context. Worker-resident projections push through the same hub via the
// BroadcastDashboardEvent Wolverine hop — a Worker-published event is handled by
// BroadcastDashboardHandler in this host and fans out to subscribed clients via the hub.
var signalR = builder.Services.AddSignalR();
if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    signalR.AddStackExchangeRedis(redisConnectionString, opts =>
        opts.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("so:signalr"));
}
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();

const string dashboardCorsPolicy = "DashboardDev";
builder.Services.AddCors(options => options.AddPolicy(dashboardCorsPolicy, policy => policy
    .WithOrigins("http://localhost:5173", "http://localhost:5174")
    .WithHeaders("X-Tenant-Id", "X-Tenant-Role", "Authorization", "Content-Type", "Accept")
    .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
    .AllowCredentials()));

var app = builder.Build();

await EnsureDatabasesAndModulesAsync(app).ConfigureAwait(false);

// Log the bound WC webhook URL at boot so we can confirm whether a running process picked
// up the latest launchSettings/appsettings overrides — IOptions snapshots are taken at
// startup and don't reflect file edits without a restart.
{
    var wcOptions = app.Services.GetRequiredService<IOptions<WooCommerceOptions>>().Value;
    app.Logger.LogInformation(
        "WooCommerce config bound: OrchestratorWebhookUrl={WebhookUrl} CallbackBaseUrl={CallbackBaseUrl}",
        wcOptions.OrchestratorWebhookUrl, wcOptions.CallbackBaseUrl);
}

app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Environment.IsDevelopment())
    app.UseCors(dashboardCorsPolicy);
app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapGet("/readyz", async (
        OrchestratorDbContext orchestrator,
        CustomerReadDbContext customerRead,
        CancellationToken ct) =>
    {
        var orchOk = await orchestrator.Database.CanConnectAsync(ct).ConfigureAwait(false);
        var customerOk = await customerRead.Database.CanConnectAsync(ct).ConfigureAwait(false);
        var body = new
        {
            status = orchOk && customerOk ? "ready" : "degraded",
            checks = new { orchestrator = orchOk ? "ok" : "down", customer_read = customerOk ? "ok" : "down" },
        };
        return orchOk && customerOk
            ? Results.Ok(body)
            : Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
    })
    .AllowAnonymous();

app.MapPublicApiEndpoints();
app.MapPerformanceProbeEndpoints(builder.Configuration, builder.Environment);
app.MapHub<RealtimeHub>("/v1/realtime").AllowAnonymous();

// Dev-only tenant picker endpoint for the customer SPA login screen on localhost.
// Listing tenants from a tenant-facing host is otherwise a clear leak; this is mapped
// strictly when the host runs as Development so the customer dashboard can pick a real
// tenant to act as during local demos. Production paths use real auth and never see this.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/v1/dev/tenants", async (
        ITenantRepository tenants,
        int? take,
        int? skip,
        CancellationToken ct) =>
    {
        var rows = await tenants.ListAsync(take ?? 50, skip ?? 0, ct).ConfigureAwait(false);
        return Results.Ok(rows.Select(t => new DevTenantSummary(
            t.Id.Value, t.DisplayName, t.Status.ToString(), t.CreatedAt)).ToArray());
    }).AllowAnonymous().WithTags("Dashboard (Customer)");
}

await app.RunAsync().ConfigureAwait(false);
return;

static async Task EnsureDatabasesAndModulesAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");
    await MigrationCoordinator.RunWithAdvisoryLockAsync(
        scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>(),
        MigrationLockKeys.Orchestrator, logger, CancellationToken.None).ConfigureAwait(false);
    await MigrationCoordinator.RunWithAdvisoryLockAsync(
        scope.ServiceProvider.GetRequiredService<CustomerReadDbContext>(),
        MigrationLockKeys.CustomerRead, logger, CancellationToken.None).ConfigureAwait(false);

    await app.Services.BootstrapConnectorModulesAsync().ConfigureAwait(false);
    app.Services.RegisterConnectorModules();
}

// WebApplicationFactory<Program> in the perf test project boots PublicApi in-process. The
// implicit Program class generated from top-level statements is internal; partial-class
// promotion makes it public so test assemblies can reach it without InternalsVisibleTo.
public partial class Program;
