using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ShippingOrchestrator.Application.Ingestion;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Infrastructure.Testing;
using ShippingOrchestrator.PerformanceTests.Driver;
using ShippingOrchestrator.PerformanceTests.Fixtures;

namespace ShippingOrchestrator.PerformanceTests.Scenarios;

/// <summary>
/// Verifies that a single noisy tenant pushing 80% of dispatch traffic does not starve the
/// other 15 tenants from reaching the publisher. Asserts every quiet tenant successfully
/// dispatches at least once over the run and per-tenant fail rate stays under 10%. The
/// guarantee breaks if the dispatcher's pre-check or the SQS publish path ever serialises
/// across tenants. Test cases scale the total shipment count.
/// </summary>
[TestFixture]
[Category(PerformanceCategory.Name)]
public class MultiTenantFairnessScenario
{

    [TestCase(10_000)]

    public async Task Noisy_tenant_does_not_starve_quiet_tenants(int totalShipments)
    {
        var ratePerSecond = Math.Clamp(totalShipments / 30, 200, 5000);
        var duration = TimeSpan.FromSeconds(Math.Max(5, Math.Ceiling(totalShipments / (double)ratePerSecond)));

        await using var stack = new PerformanceStackFixture();
        await stack.StartAsync();

        await using var factory = new PerformanceWebApplicationFactory(stack);
        using var _ = factory.CreateClient();

        var tenantIds = await PerformanceSeed
            .SeedTenantsWithShopifyConnectionsAsync(stack.OrchestratorConnectionString, tenantCount: 16, CancellationToken.None);
        var noisy = tenantIds[0];

        var perTenantOk = new ConcurrentDictionary<Guid, int>();
        var perTenantFail = new ConcurrentDictionary<Guid, int>();

        var runner = new LoadRunner(
            step: async (i, ct) =>
            {
                using var scope = factory.Services.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IIngestionDispatcher>();
                // 80/20 split: 4 of every 5 calls go to the noisy tenant; the 5th rotates
                // round-robin across the remaining 15 tenants so each gets a fair share.
                var tenantGuid = (i % 5 == 0)
                    ? tenantIds[1 + ((i / 5) % (tenantIds.Count - 1))]
                    : noisy;
                try
                {
                    var ack = await dispatcher.DispatchAsync(PerfPayloadFactory.Sample(new TenantId(tenantGuid), $"FAIR-{i}"), ct);
                    perTenantOk.AddOrUpdate(tenantGuid, 1, (_, v) => v + 1);
                    return ack.PendingOrderId != Guid.Empty;
                }
                catch
                {
                    perTenantFail.AddOrUpdate(tenantGuid, 1, (_, v) => v + 1);
                    return false;
                }
            },
            ratePerSecond: ratePerSecond,
            duration: duration,
            maxConcurrency: 64);

        var result = await runner.RunAsync($"{nameof(MultiTenantFairnessScenario)}-{totalShipments}");
        result.WriteReport(Path.Combine(AppContext.BaseDirectory, "reports", $"{nameof(MultiTenantFairnessScenario)}-{totalShipments}"));

        Assert.That(perTenantOk.Count, Is.GreaterThanOrEqualTo(tenantIds.Count),
            $"All {tenantIds.Count} tenants should successfully dispatch at least once. Saw {perTenantOk.Count}.");

        foreach (var (tenantId, ok) in perTenantOk)
        {
            var fails = perTenantFail.GetValueOrDefault(tenantId, 0);
            Assert.That(fails, Is.LessThan(Math.Max(1, ok / 10)),
                $"Tenant {tenantId:N}: {fails} failures vs {ok} successes — over 10% breaks fairness.");
        }
    }

}
