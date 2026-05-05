using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ShippingOrchestrator.Application.Ingestion;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Infrastructure.Testing;
using ShippingOrchestrator.PerformanceTests.Driver;
using ShippingOrchestrator.PerformanceTests.Fixtures;

namespace ShippingOrchestrator.PerformanceTests.Scenarios;

/// <summary>
/// Saturation counterpart to <see cref="IngestionDispatcherThroughputScenario"/>. Drives the
/// dispatcher as fast as <c>maxConcurrency</c> allows — no rate cap — until the target shipment
/// count has been issued. Achieved RPS in the report is the architectural ceiling for the
/// per-call work on this machine; future PRs that change the hot path can be compared against it.
/// </summary>
[TestFixture]
[Category(PerformanceCategory.Name)]
public class IngestionSaturationScenario
{
    [TestCase(10_000)]
    public async Task Dispatcher_saturates_at_target_shipment_count(int totalShipments)
    {
        await using var stack = new PerformanceStackFixture();
        await stack.StartAsync();

        await using var factory = new PerformanceWebApplicationFactory(stack);
        using var _ = factory.CreateClient();

        var tenantIds = await PerformanceSeed
            .SeedTenantsWithShopifyConnectionsAsync(stack.OrchestratorConnectionString, tenantCount: 16, CancellationToken.None);

        var runner = LoadRunner.Saturation(
            step: async (i, ct) =>
            {
                using var scope = factory.Services.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IIngestionDispatcher>();
                var tenant = new TenantId(tenantIds[i % tenantIds.Count]);
                var ack = await dispatcher.DispatchAsync(PerfPayloadFactory.Sample(tenant, $"SAT-{i}"), ct);
                return ack.PendingOrderId != Guid.Empty;
            },
            targetCount: totalShipments,
            maxConcurrency: 128);

        var result = await runner.RunAsync($"{nameof(IngestionSaturationScenario)}-{totalShipments}");
        result.WriteReport(Path.Combine(AppContext.BaseDirectory, "reports", $"{nameof(IngestionSaturationScenario)}-{totalShipments}"));

        Assert.That(result.FailCount, Is.LessThan(Math.Max(1, result.Total / 100)),
            "Saturation fail rate should stay under 1%.");
        Assert.That(result.OkCount, Is.GreaterThanOrEqualTo((int)(totalShipments * 0.99)),
            $"Expected ≥99% of {totalShipments} dispatches to ack OK (saw {result.OkCount}).");
        Assert.That(result.EffectiveRps, Is.GreaterThan(100),
            $"Achieved RPS should clear 100 — anything lower means the per-call cost is the ceiling.");
    }

}
