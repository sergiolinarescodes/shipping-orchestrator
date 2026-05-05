using Microsoft.Extensions.Configuration;
using Wolverine;
using Wolverine.AmazonSqs;
using Wolverine.Postgresql;
using Wolverine.Transports;

namespace ShippingOrchestrator.Infrastructure.Wolverine;

public static class WolverineConfigurationExtensions
{
    /// <summary>
    /// One transport for every environment: SQS + Postgres durable outbox/inbox.
    ///
    /// Endpoint resolution:
    ///   - <c>Aws:LocalStackPort</c> set → LocalStack on that port (dev / E2E with Testcontainers).
    ///   - otherwise (production)        → real AWS via the SDK's standard credential chain.
    /// </summary>
    public static void ConfigureOrchestratorMessaging(
        this WolverineOptions opts,
        IConfiguration configuration,
        params System.Reflection.Assembly[] additionalHandlerAssemblies)
    {
        opts.Discovery.IncludeAssembly(typeof(Application.Shipments.CreateShipmentBatchCommand).Assembly);
        foreach (var assembly in additionalHandlerAssemblies)
            opts.Discovery.IncludeAssembly(assembly);

        opts.Policies.AutoApplyTransactions();

        var connectionString = configuration.GetConnectionString("Orchestrator")
            ?? throw new InvalidOperationException("ConnectionStrings:Orchestrator is required.");
        opts.PersistMessagesWithPostgresql(connectionString, "messaging");

        var localStackPort = configuration.GetValue<int?>("Aws:LocalStackPort");
        var purgeOnStartup = configuration.GetValue("Messaging:AutoPurgeOnStartup", false);

        var sqs = localStackPort.HasValue
            ? opts.UseAmazonSqsTransportLocally(localStackPort.Value)
            : opts.UseAmazonSqsTransport();

        // LocalStack provisions queues sequentially and is sensitive to queue count.
        // Skip Wolverine's per-node system + control queues — we don't need request/reply or
        // multi-node leader election for this shape — and disable native DLQs (failures are
        // surfaced via the Postgres dead-letter table from the durability pipeline instead).
        //
        // Queue naming: derive from MESSAGE type (not handler type). Publishers like
        // PrivateApi/PublicApi don't include the ReadModels assembly in Wolverine discovery
        // (CLAUDE.md keeps projection handlers Worker-only), so they can't resolve a handler
        // type when sending — only the message type. Both sender and listener align on the
        // message type's short name. SQS also rejects dots, so the short name keeps the
        // queue name SQS-legal.
        sqs.SystemQueuesAreEnabled(false)
            .DisableAllNativeDeadLetterQueues()
            .UseConventionalRouting(NamingSource.FromMessageType, conv => conv
                .IdentifierForListener(t => t.Name)
                .IdentifierForSender(t => t.Name))
            .AutoProvision();

        // Per-tenant ingestion sharding. When configured > 1, IIngestionDispatcher hashes the
        // payload's tenant id and pins each ingest command to one of N shard queues so a noisy
        // tenant cannot starve quieter ones. Worker listens on every shard so any pod can drain
        // any tenant; deployments that want true isolation pin pods to subsets of shards via
        // labels. Default is 1 — the conventional unsharded queue stays the only path —
        // so existing deployments don't change behaviour without an explicit knob.
        var ingestionShardCount = configuration.GetValue("Messaging:IngestionShardCount", 1);
        if (ingestionShardCount > 1)
        {
            for (var i = 0; i < ingestionShardCount; i++)
                opts.ListenToSqsQueue(
                    IngestionDispatcher.ShardQueuePrefix
                    + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (purgeOnStartup)
            sqs.AutoPurgeOnStartup();
    }
}
