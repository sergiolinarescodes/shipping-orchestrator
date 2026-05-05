using System.Collections.Concurrent;
using ShippingOrchestrator.Domain.Events;

namespace ShippingOrchestrator.E2E.Tests.Wolverine;

/// <summary>
/// Singleton signalling primitive: tests await <see cref="WaitAsync"/> for a specific batch
/// id and the in-process Wolverine listener <see cref="BatchCompletionListener"/> flips
/// the corresponding <see cref="TaskCompletionSource{T}"/> when the matching domain event
/// is dispatched. Replaces the previous time-based polling loop with a deterministic wait.
/// Reset between fixtures via <see cref="Reset"/>.
/// </summary>
public sealed class BatchCompletionSignal
{
    private ConcurrentDictionary<Guid, TaskCompletionSource<ShipmentBatchCompleted>> _tcsByBatch = new();

    public Task<ShipmentBatchCompleted> WaitAsync(Guid batchId, TimeSpan timeout, CancellationToken ct = default)
    {
        var tcs = _tcsByBatch.GetOrAdd(batchId, _ => CreateSource());
        return tcs.Task.WaitAsync(timeout, ct);
    }

    public void Notify(ShipmentBatchCompleted evt)
    {
        var tcs = _tcsByBatch.GetOrAdd(evt.BatchId, _ => CreateSource());
        tcs.TrySetResult(evt);
    }

    public void Reset() => _tcsByBatch = new ConcurrentDictionary<Guid, TaskCompletionSource<ShipmentBatchCompleted>>();

    private static TaskCompletionSource<ShipmentBatchCompleted> CreateSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
