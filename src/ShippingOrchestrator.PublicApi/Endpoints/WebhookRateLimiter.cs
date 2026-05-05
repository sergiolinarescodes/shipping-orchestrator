using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace ShippingOrchestrator.PublicApi.Endpoints;

/// <summary>
/// Per-tenant rate limiter for any order-producing path — today's webhook intake, tomorrow's
/// custom REST API for direct order create/update, future bulk-import endpoints. The
/// abstraction is intentionally agnostic of <em>which</em> path is acquiring the lease:
/// callers compose their partition key via <see cref="RateLimitPartitions"/> so a single
/// bucket can be scoped per-(connector, tenant), per-tenant-globally, or anywhere in
/// between. When <c>ConnectionStrings:Redis</c> is set the implementation becomes
/// <see cref="RedisRateLimiter"/> and the cap holds globally across PublicApi pods.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Acquire one permit from the bucket identified by <paramref name="partitionKey"/>.
    /// The lease's <c>IsAcquired</c> tells the caller whether to proceed or reject. Always
    /// dispose the returned lease (use <c>using</c>) so the BCL implementation can release
    /// internal state — even leases from <see cref="RedisRateLimiter"/>, where dispose is a
    /// no-op, must be honoured to keep callers swap-safe.
    /// </summary>
    ValueTask<RateLimitLease> AcquireAsync(string partitionKey, CancellationToken cancellationToken);
}

/// <summary>
/// Standard partition-key shapes. Callers should reach for <see cref="Tenant"/> when the cap
/// is a tenant-level fairness guarantee (a noisy tenant must not starve quieter ones across
/// the entire platform) and <see cref="Connector"/> when the cap is a platform-imposed
/// per-shop ceiling (Shopify caps at ~40 req/s/shop; WooCommerce at the WP host's load
/// budget). Endpoints that participate in <em>both</em> caps (e.g. a custom-API order create
/// path that must respect the per-tenant fairness AND the tenant's chosen per-connector
/// throttle) acquire two leases — connector first, tenant second — and reject if either
/// denies. The dual-acquire pattern is documented in <c>WebhookEndpoints.cs</c>.
/// </summary>
public static class RateLimitPartitions
{
    /// <summary>Per-(connector, tenant) bucket. Use for platform-imposed caps where
    /// different connectors have independent budgets.</summary>
    public static string Connector(string connectorCode, Guid tenantId)
        => $"connector:{connectorCode}:{tenantId:N}";

    /// <summary>Per-tenant bucket shared across every order-producing path. Use when the
    /// goal is platform-level fairness rather than a connector-specific ceiling.</summary>
    public static string Tenant(Guid tenantId) => $"tenant:{tenantId:N}";

    /// <summary>Free-form escape hatch for unusual scopes (e.g. per-IP for unauthenticated
    /// probe endpoints). Callers must namespace the key themselves to avoid collisions.</summary>
    public static string Custom(string namespacedKey) => namespacedKey;
}

public sealed class WebhookRateLimitOptions
{
    public const string SectionName = "Webhooks:RateLimit";

    public int BurstCapacity { get; set; } = 40;
    public int TokensPerPeriod { get; set; } = 20;
    public int ReplenishmentPeriodSeconds { get; set; } = 1;
}

/// <summary>
/// In-memory token bucket. Suitable for single-pod dev and as a fallback when no Redis is
/// configured. Defaults match Shopify's documented per-shop ceiling (40 req/s burst,
/// 20/s sustained); the real deploy can tune via <c>Webhooks:RateLimit:*</c>.
/// </summary>
public sealed class InMemoryRateLimiter : IRateLimiter, IAsyncDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter;

    public InMemoryRateLimiter(IOptions<WebhookRateLimitOptions> options)
    {
        var o = options.Value;
        _limiter = PartitionedRateLimiter.Create<string, string>(partitionKey =>
            RateLimitPartition.GetTokenBucketLimiter(partitionKey, _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = o.BurstCapacity,
                TokensPerPeriod = o.TokensPerPeriod,
                ReplenishmentPeriod = TimeSpan.FromSeconds(o.ReplenishmentPeriodSeconds),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
    }

    public ValueTask<RateLimitLease> AcquireAsync(string partitionKey, CancellationToken cancellationToken)
        => _limiter.AcquireAsync(partitionKey, permitCount: 1, cancellationToken);

    public ValueTask DisposeAsync() => _limiter.DisposeAsync();
}
