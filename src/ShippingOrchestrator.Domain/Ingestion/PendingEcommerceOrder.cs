using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Domain.Ingestion;

/// <summary>
/// Inbox row for a normalized ecommerce order received via webhook (or admin simulator) but
/// not yet bundled into a shipment batch. The customer dashboard groups several of these into
/// a single batch on demand via <c>BundlePendingOrdersCommand</c>. This is a persistable
/// record (not an aggregate root) — there is no per-row business invariant beyond the
/// consumed/not-consumed flag, so the read+write story stays trivial.
/// </summary>
public sealed class PendingEcommerceOrder
{
    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string PlatformCode { get; private set; }
    public string ExternalOrderId { get; private set; }
    public string PayloadJson { get; private set; }
    public DateTimeOffset IngestedAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public Guid? ConsumedByBatchId { get; private set; }

    private PendingEcommerceOrder()
    {
        PlatformCode = string.Empty;
        ExternalOrderId = string.Empty;
        PayloadJson = string.Empty;
    }

    public PendingEcommerceOrder(
        Guid id,
        TenantId tenantId,
        string platformCode,
        string externalOrderId,
        string payloadJson,
        DateTimeOffset ingestedAt)
    {
        Id = id;
        TenantId = tenantId;
        PlatformCode = platformCode;
        ExternalOrderId = externalOrderId;
        PayloadJson = payloadJson;
        IngestedAt = ingestedAt;
    }

    public bool IsConsumed => ConsumedAt is not null;

    public void MarkConsumed(Guid batchId, DateTimeOffset consumedAt)
    {
        if (IsConsumed)
            throw new InvalidOperationException($"Pending order {Id} is already consumed by batch {ConsumedByBatchId}.");
        ConsumedByBatchId = batchId;
        ConsumedAt = consumedAt;
    }
}
