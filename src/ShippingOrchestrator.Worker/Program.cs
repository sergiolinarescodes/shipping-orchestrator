using Microsoft.EntityFrameworkCore;
using ShippingOrchestrator.Application;
using ShippingOrchestrator.CarrierConnectors.PostNL;
using ShippingOrchestrator.EcommerceConnectors.Shopify;
using ShippingOrchestrator.EcommerceConnectors.WooCommerce;
using ShippingOrchestrator.Infrastructure;
using ShippingOrchestrator.Infrastructure.Persistence;
using ShippingOrchestrator.Infrastructure.Telemetry;
using ShippingOrchestrator.Infrastructure.Wolverine;
using ShippingOrchestrator.Modules.Abstractions;
using ShippingOrchestrator.ReadModels;
using ShippingOrchestrator.ReadModels.Customer.Persistence;
using ShippingOrchestrator.ReadModels.Operations.Persistence;
using ShippingOrchestrator.ReadModels.Realtime;
using ShippingOrchestrator.Worker.Jobs;
using ShippingOrchestrator.Worker.Realtime;
using Wolverine;

// Worker uses WebApplication so it can expose /healthz + /readyz on a tiny Kestrel listener.
// ECS rolling deploys gate Worker traffic on /readyz once it actually drains the outbox and
// projects events; without an HTTP probe ECS only sees container exit. Wolverine, jobs, and
// projections still run as the primary work — Kestrel is just the probe surface.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOrchestratorCore(builder.Configuration);
builder.Services.AddOrchestratorApplication();
builder.Services.AddOperationsReadPlatform(builder.Configuration);
builder.Services.AddCustomerReadPlatform(builder.Configuration);
builder.Services.AddOrchestratorTelemetry(builder.Configuration, "shipping-orchestrator-worker");

builder.Services.AddConnectorModule<ShopifyConnectorModule>(builder.Configuration);
builder.Services.AddConnectorModule<WooCommerceConnectorModule>(builder.Configuration);
builder.Services.AddConnectorModule<PostNlConnectorModule>(builder.Configuration);

builder.Services.AddHostedService<IngestionFailurePurgeService>();
builder.Services.AddHostedService<OutboxLagMonitor>();

// Projection handlers running in this host call IDashboardBroadcaster after each
// customer-side commit; the impl publishes BroadcastDashboardEvent so PublicApi pods
// can fan a SignalR invalidation out to subscribed clients.
builder.Services.AddScoped<IDashboardBroadcaster, WolverineDashboardBroadcaster>();

builder.Host.UseWolverine(opts =>
    opts.ConfigureOrchestratorMessaging(
        builder.Configuration,
        typeof(ShippingOrchestrator.ReadModels.Projections.ShipmentProjectionHandler).Assembly));

builder.Services.AddProblemDetails();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");
    await MigrationCoordinator.RunWithAdvisoryLockAsync(
        scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>(),
        MigrationLockKeys.Orchestrator, logger, CancellationToken.None).ConfigureAwait(false);
    await MigrationCoordinator.RunWithAdvisoryLockAsync(
        scope.ServiceProvider.GetRequiredService<OperationsReadDbContext>(),
        MigrationLockKeys.OperationsRead, logger, CancellationToken.None).ConfigureAwait(false);
    await MigrationCoordinator.RunWithAdvisoryLockAsync(
        scope.ServiceProvider.GetRequiredService<CustomerReadDbContext>(),
        MigrationLockKeys.CustomerRead, logger, CancellationToken.None).ConfigureAwait(false);
    await app.Services.BootstrapConnectorModulesAsync().ConfigureAwait(false);
    app.Services.RegisterConnectorModules();
}

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapGet("/readyz", async (
        OrchestratorDbContext orchestrator,
        OperationsReadDbContext operationsRead,
        CustomerReadDbContext customerRead,
        CancellationToken ct) =>
    {
        var orchOk = await orchestrator.Database.CanConnectAsync(ct).ConfigureAwait(false);
        var opsOk = await operationsRead.Database.CanConnectAsync(ct).ConfigureAwait(false);
        var customerOk = await customerRead.Database.CanConnectAsync(ct).ConfigureAwait(false);
        var healthy = orchOk && opsOk && customerOk;
        var body = new
        {
            status = healthy ? "ready" : "degraded",
            checks = new
            {
                orchestrator = orchOk ? "ok" : "down",
                operations_read = opsOk ? "ok" : "down",
                customer_read = customerOk ? "ok" : "down",
            },
        };
        return healthy
            ? Results.Ok(body)
            : Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
    });

await app.RunAsync().ConfigureAwait(false);
