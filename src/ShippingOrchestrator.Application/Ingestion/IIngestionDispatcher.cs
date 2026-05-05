using ShippingOrchestrator.Modules.Abstractions.Ecommerce;

namespace ShippingOrchestrator.Application.Ingestion;

/// <summary>
/// Single seam for sending an <see cref="IngestEcommerceOrderCommand"/>. Webhook intake
/// (PublicApi) and the admin simulator (PrivateApi) call through here instead of resolving
/// <c>IMessageBus</c> directly. The default implementation pre-allocates a pending order id,
/// short-circuits with <see cref="IngestionAck.AlreadyPending"/> when the unique index would
/// have rejected the insert, and otherwise publishes the command fire-and-forget through SQS
/// so a noisy tenant cannot starve a publishing host's request thread.
///
/// When <c>Messaging:IngestionShardCount</c> is set greater than 1, the dispatcher hashes the
/// payload's tenant id into one of N shard queues so the queue depth itself is partitioned and
/// each Worker pod can be pinned to a subset of shards if true tenant isolation is required.
/// </summary>
public interface IIngestionDispatcher
{
    Task<IngestionAck> DispatchAsync(
        EcommerceOrderPayload payload, CancellationToken cancellationToken);
}
