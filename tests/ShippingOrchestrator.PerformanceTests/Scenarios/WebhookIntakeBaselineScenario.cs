using NUnit.Framework;
using ShippingOrchestrator.PerformanceTests.Driver;
using ShippingOrchestrator.PerformanceTests.Fixtures;

namespace ShippingOrchestrator.PerformanceTests.Scenarios;

/// <summary>
/// Drives a fixed RPS at PublicApi's anonymous <c>/healthz</c> to characterise the request
/// pipeline's idle floor — no DB hit, no SQS publish, no rate limiter. Establishes the
/// latency floor every other scenario should stay within an order of magnitude of, and
/// doubles as a smoke test that the harness, Testcontainers stack, and WebApplicationFactory
/// boot work end-to-end. Test cases scale the target request count.
/// </summary>
[TestFixture]
[Category(PerformanceCategory.Name)]
public class WebhookIntakeBaselineScenario
{

    [TestCase(10_000)]

    public async Task Healthz_holds_target_request_count_without_failures(int totalRequests)
    {
        var ratePerSecond = Math.Clamp(totalRequests / 30, 500, 10_000);
        var duration = TimeSpan.FromSeconds(Math.Max(5, Math.Ceiling(totalRequests / (double)ratePerSecond)));

        await using var stack = new PerformanceStackFixture();
        await stack.StartAsync();

        await using var factory = new PerformanceWebApplicationFactory(stack);
        using var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
            await client.GetAsync("/healthz").ConfigureAwait(false);

        var runner = new LoadRunner(
            step: async (_, ct) =>
            {
                var response = await client.GetAsync("/healthz", ct).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            },
            ratePerSecond: ratePerSecond,
            duration: duration,
            maxConcurrency: 256);

        var result = await runner.RunAsync($"{nameof(WebhookIntakeBaselineScenario)}-{totalRequests}");
        result.WriteReport(Path.Combine(AppContext.BaseDirectory, "reports", $"{nameof(WebhookIntakeBaselineScenario)}-{totalRequests}"));

        Assert.That(result.FailCount, Is.EqualTo(0), "No 5xx allowed at any scale.");
        Assert.That(result.OkCount, Is.GreaterThanOrEqualTo((int)(totalRequests * 0.95)),
            $"Expected ≥95% of {totalRequests} requests to land successfully (saw {result.OkCount}).");
        Assert.That(result.P99Ms, Is.LessThan(2_000),
            "p99 latency for /healthz should stay under 2s on a laptop.");
    }
}
