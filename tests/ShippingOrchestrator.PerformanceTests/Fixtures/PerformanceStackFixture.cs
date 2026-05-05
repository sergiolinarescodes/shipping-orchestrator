using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.LocalStack;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace ShippingOrchestrator.PerformanceTests.Fixtures;

/// <summary>
/// Boots the three pieces of infrastructure every perf scenario needs — Postgres
/// (orchestrator + read schemas), LocalStack (SQS for the Wolverine outbox), Redis (SignalR
/// backplane + distributed rate limiter). Scenarios pull connection strings off this fixture
/// and pass them to the in-process PublicApi via <c>WebApplicationFactory</c>.
///
/// Lifetime is per-fixture (one-time setup / teardown). Containers are torn down at the end
/// of the run; intermediate state is reset by the scenario itself (Respawn for Postgres,
/// SQS purge, Redis FLUSHDB) so each scenario starts from a known baseline.
/// </summary>
public sealed class PerformanceStackFixture : IAsyncDisposable
{
#pragma warning disable CS0618 // Parameterless ctor is obsolete; image is overridden via WithImage below — same pattern as E2EFixture.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
#pragma warning restore CS0618
        .WithImage("postgres:17-alpine")
        .WithDatabase("shipping_orchestrator_perf")
        .WithUsername("app")
        .WithPassword("app_dev_password")
        .WithLogger(NullLogger.Instance)
        .Build();

    private readonly LocalStackContainer _localstack = new LocalStackBuilder()
        .WithImage("localstack/localstack:3")
        .WithEnvironment("SERVICES", "sqs,sns")
        .WithEnvironment("DEFAULT_REGION", "eu-west-1")
        .WithLogger(NullLogger.Instance)
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .WithLogger(NullLogger.Instance)
        .Build();

    public string OrchestratorConnectionString => _postgres.GetConnectionString();

    public int LocalStackPort => _localstack.GetMappedPublicPort(4566);

    public string RedisConnectionString => _redis.GetConnectionString();

    public async Task StartAsync()
    {
        await Task.WhenAll(
            _postgres.StartAsync(),
            _localstack.StartAsync(),
            _redis.StartAsync()).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync().ConfigureAwait(false);
        await _localstack.DisposeAsync().ConfigureAwait(false);
        await _redis.DisposeAsync().ConfigureAwait(false);
    }
}
