using Microsoft.Extensions.Logging;
using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using Wolverine;

namespace ShippingOrchestrator.Application.Shipments;

public static class ProcessShipmentBatchHandler
{
    public static async Task Handle(
        ProcessShipmentBatchCommand command,
        IShipmentBatchRepository batches,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        IClock clock,
        ILogger<ProcessShipmentBatchHandlerMarker> log,
        CancellationToken cancellationToken)
    {
        var batch = await batches.FindAsync(command.BatchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            log.LogWarning("Batch {BatchId} not found while processing.", command.BatchId);
            return;
        }
        batch.StartProcessing(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var item in batch.Items)
            await bus.PublishAsync(new RequestCarrierLabelCommand(batch.Id, item.ShipmentId)).ConfigureAwait(false);
    }
}

/// <summary>Marker type used purely for typed <see cref="ILogger{T}"/> resolution.</summary>
public sealed class ProcessShipmentBatchHandlerMarker;
