using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ShippingOrchestrator.Application.Ingestion;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Infrastructure.Testing;
using ShippingOrchestrator.PerformanceTests.Driver;
using ShippingOrchestrator.PerformanceTests.Fixtures;

namespace ShippingOrchestrator.PerformanceTests.Scenarios;

/// <summary>
/// Direct exercise of the fire-and-forget ingest path: resolves <see cref="IIngestionDispatcher"/>
/// from the in-process service provider and fires N dispatch calls across 16 seeded tenants.
/// Bypasses HMAC + the Kestrel pipeline so the measured surface is purely the publisher's
/// pre-check + SQS send. Test cases scale the total shipment count.
/// </summary>
[TestFixture]
[Category(PerformanceCategory.Name)]
public class IngestionDispatcherThroughputScenario
{

    [TestCase(10_000)]

    public async Task Dispatcher_holds_publish_throughput_for_target_shipment_count(int totalShipments)
    {
        var ratePerSecond = Math.Clamp(totalShipments / 30, 200, 5000);
        var duration = TimeSpan.FromSeconds(Math.Max(5, Math.Ceiling(totalShipments / (double)ratePerSecond)));

        await using var stack = new PerformanceStackFixture();
        await stack.StartAsync();

        await using var factory = new PerformanceWebApplicationFactory(stack);
        using var _ = factory.CreateClient();

        var tenantIds = await PerformanceSeed
            .SeedTenantsWithShopifyConnectionsAsync(stack.OrchestratorConnectionString, tenantCount: 16, CancellationToken.None);

        var runner = new LoadRunner(
            step: async (i, ct) =>
            {
                using var scope = factory.Services.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IIngestionDispatcher>();
                var tenant = new TenantId(tenantIds[i % tenantIds.Count]);
                var ack = await dispatcher.DispatchAsync(PerfPayloadFactory.Sample(tenant, $"DISP-{i}"), ct);
                return ack.PendingOrderId != Guid.Empty;
            },
            ratePerSecond: ratePerSecond,
            duration: duration,
            maxConcurrency: 64);

        var result = await runner.RunAsync($"{nameof(IngestionDispatcherThroughputScenario)}-{totalShipments}");
        result.WriteReport(Path.Combine(AppContext.BaseDirectory, "reports", $"{nameof(IngestionDispatcherThroughputScenario)}-{totalShipments}"));

        Assert.That(result.FailCount, Is.LessThan(Math.Max(1, result.Total / 100)),
            "Dispatcher fail rate should stay under 1% across the run.");
        Assert.That(result.OkCount, Is.GreaterThanOrEqualTo((int)(totalShipments * 0.95)),
            $"Expected ≥95% of {totalShipments} dispatches to ack OK (saw {result.OkCount}).");
        Assert.That(result.P99Ms, Is.LessThan(5_000),
            "p99 dispatcher latency should stay under 5s (DB pre-check + SQS round trip).");
    }

}
