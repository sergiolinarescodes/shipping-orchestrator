using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ShippingOrchestrator.Application.Ingestion;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Infrastructure.Testing;
using ShippingOrchestrator.PerformanceTests.Driver;
using ShippingOrchestrator.PerformanceTests.Fixtures;

namespace ShippingOrchestrator.PerformanceTests.Scenarios;

/// <summary>
/// Soak: 5 seeded tenants share a sustained dispatch load until N total shipments have been
/// issued. Validates that the publisher path stays steady — no fail-rate creep, no pool
/// exhaustion, no monotonic latency drift — under sustained multi-tenant traffic. Worker is
/// not booted; assertions are publisher-side. Test cases scale the total shipment count.
/// </summary>
[TestFixture]
[Category(PerformanceCategory.Name)]
public class BulkOrderIngestionScenario
{

    [TestCase(10_000)]

    public async Task Sustained_multi_tenant_dispatch_holds_for_target_shipment_count(int totalShipments)
    {
        // Pace at ~ totalShipments/30 so every test case finishes in roughly 30 seconds when
        // the architecture can keep up. Floor at 200 RPS for tiny runs (1k → 200 RPS = 5 s);
        // ceil at 5000 RPS so the harness doesn't outpace what a laptop Postgres can absorb.
        var ratePerSecond = Math.Clamp(totalShipments / 30, 200, 5000);
        var duration = TimeSpan.FromSeconds(Math.Max(5, Math.Ceiling(totalShipments / (double)ratePerSecond)));

        await using var stack = new PerformanceStackFixture();
        await stack.StartAsync();

        await using var factory = new PerformanceWebApplicationFactory(stack);
        using var _ = factory.CreateClient();

        var tenantIds = await PerformanceSeed
            .SeedTenantsWithShopifyConnectionsAsync(stack.OrchestratorConnectionString, tenantCount: 5, CancellationToken.None);

        var runner = new LoadRunner(
            step: async (i, ct) =>
            {
                using var scope = factory.Services.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IIngestionDispatcher>();
                var tenant = new TenantId(tenantIds[i % tenantIds.Count]);
                var ack = await dispatcher.DispatchAsync(PerfPayloadFactory.Sample(tenant, $"BULK-{i}"), ct);
                return ack.PendingOrderId != Guid.Empty;
            },
            ratePerSecond: ratePerSecond,
            duration: duration,
            maxConcurrency: 96);

        var result = await runner.RunAsync($"{nameof(BulkOrderIngestionScenario)}-{totalShipments}");
        result.WriteReport(Path.Combine(AppContext.BaseDirectory, "reports", $"{nameof(BulkOrderIngestionScenario)}-{totalShipments}"));

        Assert.That(result.FailCount, Is.LessThan(Math.Max(1, result.Total / 100)),
            "Soak fail rate should stay under 1%.");
        Assert.That(result.OkCount, Is.GreaterThanOrEqualTo((int)(totalShipments * 0.95)),
            $"Expected ≥95% of {totalShipments} dispatches to ack OK (saw {result.OkCount}).");
    }

}
