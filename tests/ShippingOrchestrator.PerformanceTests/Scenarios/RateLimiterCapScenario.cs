using Microsoft.Extensions.Options;
using NUnit.Framework;
using ShippingOrchestrator.PerformanceTests.Driver;
using ShippingOrchestrator.PerformanceTests.Fixtures;
using ShippingOrchestrator.PublicApi.Endpoints;
using StackExchange.Redis;

namespace ShippingOrchestrator.PerformanceTests.Scenarios;

/// <summary>
/// Validates the distributed token bucket: with <c>BurstCapacity=10</c> and 2 tokens/sec
/// refill, a 5-second 100 RPS burst should accept ~20 calls — the burst plus 5 seconds of
/// refill — and reject the rest. Runs against the Redis container in the fixture so the
/// Lua script's atomic decrement is exercised exactly the same way a multi-pod deployment
/// would. The in-memory variant is covered by the unit tests in the limiter project.
/// </summary>
[TestFixture]
[Category(PerformanceCategory.Name)]
public class RateLimiterCapScenario
{
    [Test]
    public async Task Redis_limiter_enforces_burst_plus_refill_cap()
    {
        await using var stack = new PerformanceStackFixture();
        await stack.StartAsync();

        await using var redis = await ConnectionMultiplexer.ConnectAsync(stack.RedisConnectionString);
        var options = Options.Create(new WebhookRateLimitOptions
        {
            BurstCapacity = 10,
            TokensPerPeriod = 2,
            ReplenishmentPeriodSeconds = 1,
        });
        var limiter = new RedisRateLimiter(redis, options);
        var partitionKey = RateLimitPartitions.Tenant(Guid.NewGuid());

        var runner = new LoadRunner(
            step: async (_, ct) =>
            {
                using var lease = await limiter.AcquireAsync(partitionKey, ct);
                return lease.IsAcquired;
            },
            ratePerSecond: 100,
            duration: TimeSpan.FromSeconds(5),
            maxConcurrency: 32);

        var result = await runner.RunAsync(nameof(RateLimiterCapScenario));
        result.WriteReport(Path.Combine(AppContext.BaseDirectory, "reports", nameof(RateLimiterCapScenario)));

        // Expected acquired ≈ Burst (10) + Refill rate (2/s) × duration (5s) = 20.
        // Allow ±50% tolerance for timing jitter and the LoadRunner's dispatch interval drift.
        var expected = options.Value.BurstCapacity + options.Value.TokensPerPeriod * 5;
        Assert.That(result.OkCount, Is.InRange(expected / 2, expected * 2),
            $"Acquired {result.OkCount} leases; expected ~{expected} (burst {options.Value.BurstCapacity} + refill {options.Value.TokensPerPeriod}/s × 5s).");
        Assert.That(result.Total - result.OkCount, Is.GreaterThan(result.OkCount),
            "Rejected calls should outnumber accepted ones at 100 RPS over a 20-token cap.");
    }
}
