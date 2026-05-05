using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using ShippingOrchestrator.Application;
using ShippingOrchestrator.CarrierConnectors.PostNL;
using ShippingOrchestrator.EcommerceConnectors.Shopify;
using ShippingOrchestrator.EcommerceConnectors.WooCommerce;
using ShippingOrchestrator.Infrastructure;
using ShippingOrchestrator.Infrastructure.Persistence;
using ShippingOrchestrator.Infrastructure.Telemetry;
using ShippingOrchestrator.Infrastructure.Wolverine;
using ShippingOrchestrator.Modules.Abstractions;
using ShippingOrchestrator.PrivateApi;
using ShippingOrchestrator.PrivateApi.Endpoints;
using ShippingOrchestrator.ReadModels;
using ShippingOrchestrator.ReadModels.Operations.Persistence;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOrchestratorCore(builder.Configuration);
builder.Services.AddOrchestratorApplication();
// PrivateApi only reads from the ops schema — opt into the replica when configured so heavy
// operator dashboard queries don't compete with the projection writes on the primary.
builder.Services.AddOperationsReadPlatform(builder.Configuration, preferReadReplica: true);
builder.Services.AddOrchestratorTelemetry(builder.Configuration, "shipping-orchestrator-private-api");

builder.Services.AddConnectorModule<ShopifyConnectorModule>(builder.Configuration);
builder.Services.AddConnectorModule<WooCommerceConnectorModule>(builder.Configuration);
builder.Services.AddConnectorModule<PostNlConnectorModule>(builder.Configuration);

builder.Services.AddPrivateApiStaffAuth(builder.Environment);
builder.Services.Configure<HostingOptions>(builder.Configuration.GetSection(HostingOptions.SectionName));

builder.Host.UseWolverine(opts => opts.ConfigureOrchestratorMessaging(builder.Configuration));

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

const string dashboardCorsPolicy = "DashboardDev";
builder.Services.AddCors(options => options.AddPolicy(dashboardCorsPolicy, policy => policy
    .WithOrigins("http://localhost:5174")
    .WithHeaders("X-Staff-Role", "X-Staff-User", "Authorization", "Content-Type", "Accept")
    .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")));

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
    await app.Services.BootstrapConnectorModulesAsync().ConfigureAwait(false);
    app.Services.RegisterConnectorModules();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Environment.IsDevelopment())
    app.UseCors(dashboardCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapGet("/readyz", async (
        OrchestratorDbContext orchestrator,
        OperationsReadDbContext operationsRead,
        CancellationToken ct) =>
    {
        var orchOk = await orchestrator.Database.CanConnectAsync(ct).ConfigureAwait(false);
        var opsOk = await operationsRead.Database.CanConnectAsync(ct).ConfigureAwait(false);
        var body = new
        {
            status = orchOk && opsOk ? "ready" : "degraded",
            checks = new { orchestrator = orchOk ? "ok" : "down", operations_read = opsOk ? "ok" : "down" },
        };
        return orchOk && opsOk
            ? Results.Ok(body)
            : Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
    })
    .AllowAnonymous();

app.MapPrivateApiEndpoints();

await app.RunAsync().ConfigureAwait(false);
