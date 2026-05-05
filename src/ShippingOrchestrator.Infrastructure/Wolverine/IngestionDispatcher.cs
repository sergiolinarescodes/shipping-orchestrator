using Microsoft.Extensions.Configuration;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Application.Ingestion;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;
using Wolverine;

namespace ShippingOrchestrator.Infrastructure.Wolverine;

/// <summary>
/// Wolverine-backed <see cref="IIngestionDispatcher"/>. The dispatcher is the seam that turns
/// the synchronous webhook contract (caller wants a pending-order id back) into the async
/// transport contract (handler runs on a Worker pod after a Wolverine hop). Two paths:
///
/// <list type="number">
/// <item>Pre-check the unique <c>(tenantId, platform, externalOrderId)</c> index. If a row
/// already exists, return <see cref="IngestionAck"/> with <c>AlreadyPending: true</c> and the
/// existing id without publishing — preserves today's idempotent webhook 202 behaviour for a
/// retried Shopify delivery.</item>
/// <item>Otherwise pre-allocate a fresh pending id, publish
/// <see cref="IngestEcommerceOrderCommand"/> fire-and-forget through SQS, return
/// <c>AlreadyPending: false</c> with the pre-allocated id immediately. Worker drains the
/// queue, persists the row with that id, and emits projection updates.</item>
/// </list>
///
/// When <c>Messaging:IngestionShardCount</c> &gt; 1 the publish targets a tenant-pinned shard
/// queue (FNV-1a hash of the tenant id mod shardCount). The handler still runs the same
/// existing-row check on the receiving side so any race between the dispatcher pre-check and
/// the Worker commit converges via the Postgres unique index.
/// </summary>
internal sealed class IngestionDispatcher : IIngestionDispatcher
{
    public const string ShardQueuePrefix = "IngestEcommerceOrderCommand-shard-";

    private readonly IMessageBus _bus;
    private readonly IPendingEcommerceOrderRepository _pendingRepo;
    private readonly int _shardCount;

    public IngestionDispatcher(
        IMessageBus bus,
        IPendingEcommerceOrderRepository pendingRepo,
        IConfiguration configuration)
    {
        _bus = bus;
        _pendingRepo = pendingRepo;
        var configured = configuration.GetValue("Messaging:IngestionShardCount", 1);
        _shardCount = configured > 1 ? configured : 1;
    }

    public async Task<IngestionAck> DispatchAsync(
        EcommerceOrderPayload payload, CancellationToken cancellationToken)
    {
        var existing = await _pendingRepo
            .FindByExternalIdAsync(payload.TenantId, payload.ConnectorCode, payload.ExternalOrderId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
            return new IngestionAck(existing.Id, AlreadyPending: true);

        var pendingId = Guid.NewGuid();
        var command = new IngestEcommerceOrderCommand(payload, pendingId);

        if (_shardCount <= 1)
        {
            await _bus.SendAsync(command).ConfigureAwait(false);
        }
        else
        {
            var shard = ResolveShard(payload.TenantId.Value, _shardCount);
            var queue = ShardQueuePrefix + shard.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await _bus.EndpointFor(queue).SendAsync(command).ConfigureAwait(false);
        }

        return new IngestionAck(pendingId, AlreadyPending: false);
    }

    public static int ResolveShard(Guid tenantId, int shardCount)
    {
        Span<byte> bytes = stackalloc byte[16];
        tenantId.TryWriteBytes(bytes);
        // FNV-1a — stable, deterministic hash so the same tenant always lands in the same shard.
        uint hash = 2166136261u;
        for (var i = 0; i < bytes.Length; i++)
        {
            hash ^= bytes[i];
            hash *= 16777619u;
        }
        return (int)(hash % (uint)shardCount);
    }
}
