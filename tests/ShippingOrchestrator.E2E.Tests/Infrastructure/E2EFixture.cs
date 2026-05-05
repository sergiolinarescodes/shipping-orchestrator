using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Respawn;
using ShippingOrchestrator.E2E.Tests.Infrastructure;
using ShippingOrchestrator.E2E.Tests.Wolverine;
using ShippingOrchestrator.Infrastructure.Persistence;
using ShippingOrchestrator.ReadModels.Customer.Persistence;
using ShippingOrchestrator.ReadModels.Operations.Persistence;
using Testcontainers.LocalStack;
using Testcontainers.PostgreSql;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

// Declared in the assembly's root test namespace so NUnit's [SetUpFixture] covers the
// fixture's own namespace plus every nested namespace (Infrastructure, etc.). Putting it
// inside Infrastructure would scope it only to that sub-namespace and the OneTimeSetUp
// would never run for tests living directly under ShippingOrchestrator.E2E.Tests.
namespace ShippingOrchestrator.E2E.Tests;

/// <summary>
/// Assembly-level fixture: spins up Postgres + WireMock once per test run, builds the
/// composite host once, exposes its TestServer to all tests via <see cref="HttpClient"/>.
/// Per-test isolation is delegated to <see cref="ResetAsync"/> + Respawn (called from
/// <see cref="E2ETestBase.PerTestSetUp"/>), so data does not leak across fixtures.
/// </summary>
[SetUpFixture]
public sealed class E2EFixture
{
    public static E2EFixture Current { get; private set; } = null!;

    public PostgreSqlContainer Postgres { get; private set; } = null!;
    public LocalStackContainer LocalStack { get; private set; } = null!;
    public WireMockServer Shopify { get; private set; } = null!;
    public WebApplication App { get; private set; } = null!;
    public HttpClient HttpClient => _httpClient ??= App.GetTestServer().CreateClient();
    public BatchCompletionSignal BatchSignal => App.Services.GetRequiredService<BatchCompletionSignal>();

    private HttpClient? _httpClient;
    private Respawner _respawner = null!;
    private string _connectionString = null!;
    private int _localStackPort;
    private AmazonSQSClient _sqs = null!;

    [OneTimeSetUp]
    public async Task GlobalSetUp()
    {
        Current = this;

#pragma warning disable CS0618 // Parameterless ctor is obsolete; image is overridden via WithImage below.
        Postgres = new PostgreSqlBuilder()
#pragma warning restore CS0618
            .WithImage("postgres:17-alpine")
            .WithDatabase("shipping_orchestrator_e2e")
            .WithUsername("e2e")
            .WithPassword("e2e_pwd")
            .Build();

        LocalStack = new LocalStackBuilder()
            .WithImage("localstack/localstack:3")
            .WithEnvironment("SERVICES", "sqs,sns")
            .WithEnvironment("DEFAULT_REGION", "us-east-1")
            .Build();

        await Task.WhenAll(Postgres.StartAsync(), LocalStack.StartAsync()).ConfigureAwait(false);

        Shopify = WireMockServer.Start();
        StubShopifyOAuth(Shopify);

        _connectionString = Postgres.GetConnectionString();
        _localStackPort = LocalStack.GetMappedPublicPort(4566);

        App = await E2ECompositeHost.BuildAsync(_connectionString, _localStackPort, Shopify.Url!).ConfigureAwait(false);
        await App.StartAsync().ConfigureAwait(false);

        _sqs = new AmazonSQSClient(
            new BasicAWSCredentials("test", "test"),
            new AmazonSQSConfig
            {
                ServiceURL = $"http://localhost:{_localStackPort}",
                AuthenticationRegion = "us-east-1",
            });

        await using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = new[]
            {
                OrchestratorDbContext.SchemaName,
                CustomerReadDbContext.SchemaName,
                OperationsReadDbContext.SchemaName,
                "messaging",
            },
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Truncates every test-managed schema between fixtures so data does not leak across
    /// tests. Called from <see cref="E2ETestBase.PerTestSetUp"/>.
    /// </summary>
    public async Task ResetAsync()
    {
        await using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await _respawner.ResetAsync(connection).ConfigureAwait(false);
        await PurgeAllSqsQueuesAsync().ConfigureAwait(false);
        BatchSignal.Reset();
    }

    /// <summary>
    /// Stub Shopify's OAuth token-exchange so the Real-mode connector's HTTP call resolves
    /// without leaving the test runner. Returns a fake bearer the rest of the pipeline never
    /// inspects beyond persisting it as encrypted credentials.
    /// </summary>
    private static void StubShopifyOAuth(WireMockServer wiremock)
    {
        wiremock
            .Given(Request.Create().WithPath("/admin/oauth/access_token").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"access_token\":\"shpat_e2e_test\",\"scope\":\"read_orders,write_fulfillments\"}"));
    }

    private async Task PurgeAllSqsQueuesAsync()
    {
        var queues = await _sqs.ListQueuesAsync(new ListQueuesRequest { MaxResults = 1000 }).ConfigureAwait(false);
        foreach (var url in queues.QueueUrls ?? new List<string>())
        {
            try
            {
                await _sqs.PurgeQueueAsync(new PurgeQueueRequest { QueueUrl = url }).ConfigureAwait(false);
            }
            catch (PurgeQueueInProgressException)
            {
                // SQS allows one purge per queue per 60s; for back-to-back tests we just
                // accept the prior purge already covers us.
            }
        }
    }

    [OneTimeTearDown]
    public async Task GlobalTearDown()
    {
        _httpClient?.Dispose();
        await App.DisposeAsync().ConfigureAwait(false);
        Shopify.Stop();
        Shopify.Dispose();
        _sqs.Dispose();
        await Task.WhenAll(
            Postgres.DisposeAsync().AsTask(),
            LocalStack.DisposeAsync().AsTask()).ConfigureAwait(false);
    }
}
