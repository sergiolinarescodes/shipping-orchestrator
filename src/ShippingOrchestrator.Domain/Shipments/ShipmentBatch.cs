using ShippingOrchestrator.Domain.Abstractions;
using ShippingOrchestrator.Domain.Events;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Domain.ValueObjects;

namespace ShippingOrchestrator.Domain.Shipments;

public sealed class ShipmentBatch : AggregateRoot
{
    private readonly List<ShipmentBatchItem> _items = [];

    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public ShipmentBatchStatus Status { get; private set; }
    public IdempotencyKey? IdempotencyKey { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public IReadOnlyList<ShipmentBatchItem> Items => _items;

    private ShipmentBatch() { }

    public static ShipmentBatch Accept(
        TenantId tenantId,
        IdempotencyKey? idempotencyKey,
        IEnumerable<Guid> shipmentIds,
        DateTimeOffset now) =>
        AcceptWithId(Guid.NewGuid(), tenantId, idempotencyKey, shipmentIds, now);

    /// <summary>
    /// Variant that lets the caller pre-allocate the batch id so child <see cref="Shipment"/>
    /// aggregates can reference it on creation, without needing a two-phase save. Used by the
    /// CreateShipmentBatchHandler so each shipment is born with the right BatchId — the read
    /// platform's projection can then index shipments by batch immediately.
    /// </summary>
    public static ShipmentBatch AcceptWithId(
        Guid id,
        TenantId tenantId,
        IdempotencyKey? idempotencyKey,
        IEnumerable<Guid> shipmentIds,
        DateTimeOffset now)
    {
        var batch = new ShipmentBatch
        {
            Id = id,
            TenantId = tenantId,
            Status = ShipmentBatchStatus.Pending,
            IdempotencyKey = idempotencyKey,
            CreatedAt = now,
        };
        var index = 0;
        foreach (var shipmentId in shipmentIds)
            batch._items.Add(ShipmentBatchItem.Pending(batch.Id, shipmentId, index++));
        if (batch._items.Count == 0)
            throw new ArgumentException("Batch must contain at least one shipment.", nameof(shipmentIds));
        batch.Raise(new ShipmentBatchAccepted(batch.Id, tenantId, batch._items.Count, now));
        return batch;
    }

    public void StartProcessing(DateTimeOffset now)
    {
        if (Status != ShipmentBatchStatus.Pending) return;
        Status = ShipmentBatchStatus.Processing;
        Raise(new ShipmentBatchStartedProcessing(Id, TenantId, now));
    }

    public void RecordItemSucceeded(Guid shipmentId, DateTimeOffset now)
    {
        var item = FindItem(shipmentId);
        item.MarkSucceeded(now);
        TryCompleteIfDone(now);
    }

    public void RecordItemFailed(Guid shipmentId, string reason, DateTimeOffset now)
    {
        var item = FindItem(shipmentId);
        item.MarkFailed(reason, now);
        TryCompleteIfDone(now);
    }

    /// <summary>
    /// Re-evaluates batch completion based on the currently-loaded <see cref="Items"/> snapshot.
    /// Used by handlers that loaded the batch fresh after concurrent peers already resolved
    /// their items: the very last writer flips the batch to a terminal state. Idempotent —
    /// once the batch has reached a terminal state subsequent calls do nothing.
    /// </summary>
    public void RecheckCompletion(DateTimeOffset now) => TryCompleteIfDone(now);

    private ShipmentBatchItem FindItem(Guid shipmentId) =>
        _items.FirstOrDefault(i => i.ShipmentId == shipmentId)
        ?? throw new InvalidOperationException($"Shipment {shipmentId} is not part of batch {Id}.");

    private void TryCompleteIfDone(DateTimeOffset now)
    {
        if (Status is ShipmentBatchStatus.Completed
            or ShipmentBatchStatus.Failed
            or ShipmentBatchStatus.PartiallyFailed) return;
        if (_items.Any(i => i.Status == ShipmentBatchItemStatus.Pending)) return;
        var failed = _items.Count(i => i.Status == ShipmentBatchItemStatus.Failed);
        var succeeded = _items.Count - failed;
        Status = (failed, succeeded) switch
        {
            (0, _) => ShipmentBatchStatus.Completed,
            (_, 0) => ShipmentBatchStatus.Failed,
            _ => ShipmentBatchStatus.PartiallyFailed,
        };
        CompletedAt = now;
        Raise(new ShipmentBatchCompleted(Id, TenantId, Status, succeeded, failed, now));
    }
}
