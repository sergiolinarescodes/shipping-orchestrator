using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ShippingOrchestrator.PublicApi.Endpoints;

/// <summary>
/// Distributed token bucket backed by a Redis Lua script — the bucket state
/// (current tokens + last refill timestamp) lives in two Redis keys per partition, and a
/// single atomic <c>EVAL</c> reads, refills based on elapsed time since the last call,
/// decrements, and persists in one round trip. Multi-pod deployments converge on a single
/// global cap regardless of which pod the request lands on.
///
/// Keys carry a 60s TTL refreshed on every call so an idle partition evicts itself; the
/// script re-seeds the bucket at full capacity on the next request, matching the in-memory
/// token bucket's "you start with a full burst when traffic resumes" behaviour. Partition
/// keys are caller-supplied (see <see cref="RateLimitPartitions"/>) so the same Redis
/// limiter serves webhook intake, the future custom-API order path, bulk import, etc.
/// </summary>
public sealed class RedisRateLimiter : IRateLimiter
{
    private const string BucketScript = @"
local tokens_key = KEYS[1]
local ts_key     = KEYS[2]
local capacity   = tonumber(ARGV[1])
local rate       = tonumber(ARGV[2])
local now        = tonumber(ARGV[3])
local last       = tonumber(redis.call('GET', ts_key) or now)
local current    = tonumber(redis.call('GET', tokens_key) or capacity)
local elapsed_s  = (now - last) / 1000.0
current = math.min(capacity, current + elapsed_s * rate)
if current < 1 then
  redis.call('SET', ts_key, now, 'PX', 60000)
  redis.call('SET', tokens_key, current, 'PX', 60000)
  return 0
end
current = current - 1
redis.call('SET', ts_key, now, 'PX', 60000)
redis.call('SET', tokens_key, current, 'PX', 60000)
return 1
";

    private readonly IConnectionMultiplexer _redis;
    private readonly WebhookRateLimitOptions _options;
    private readonly double _ratePerSecond;

    public RedisRateLimiter(IConnectionMultiplexer redis, IOptions<WebhookRateLimitOptions> options)
    {
        _redis = redis;
        _options = options.Value;
        var period = Math.Max(1, _options.ReplenishmentPeriodSeconds);
        _ratePerSecond = _options.TokensPerPeriod / (double)period;
    }

    public async ValueTask<RateLimitLease> AcquireAsync(string partitionKey, CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        var keys = new RedisKey[]
        {
            $"so:rl:{partitionKey}:tokens",
            $"so:rl:{partitionKey}:ts",
        };
        var args = new RedisValue[]
        {
            _options.BurstCapacity,
            _ratePerSecond.ToString(CultureInfo.InvariantCulture),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        // ScriptEvaluateAsync has no CancellationToken overload; WaitAsync at least frees the
        // awaiter when the request aborts (the in-flight Redis call still runs to completion).
        var result = await db.ScriptEvaluateAsync(BucketScript, keys, args)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        return new SimpleLease(acquired: (long)result == 1);
    }

    private sealed class SimpleLease : RateLimitLease
    {
        private readonly bool _acquired;

        public SimpleLease(bool acquired) => _acquired = acquired;

        public override bool IsAcquired => _acquired;

        public override IEnumerable<string> MetadataNames => Array.Empty<string>();

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = null;
            return false;
        }
    }
}
