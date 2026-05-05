using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Application.Routing;
using ShippingOrchestrator.Domain.Shipments;
using ShippingOrchestrator.Modules.Abstractions;
using ShippingOrchestrator.Modules.Abstractions.Carriers;
using Wolverine;

namespace ShippingOrchestrator.Application.Shipments;

public sealed record RequestCarrierLabelCommand(Guid BatchId, Guid ShipmentId);

public static class RequestCarrierLabelHandler
{
    public static async Task Handle(
        RequestCarrierLabelCommand command,
        IShipmentRepository shipments,
        IShipmentBatchRepository batches,
        RoutingEngine routing,
        ConnectorRegistry registry,
        IServiceProvider serviceProvider,
        IUnitOfWork unitOfWork,
        IClock clock,
        IMessageBus bus,
        ILogger<RequestCarrierLabelMarker> log,
        CancellationToken cancellationToken)
    {
        try
        {
        var shipment = await shipments.FindAsync(command.ShipmentId, cancellationToken).ConfigureAwait(false);
        if (shipment is null)
        {
            log.LogWarning("Shipment {ShipmentId} not found.", command.ShipmentId);
            return;
        }

        var batch = await batches.FindAsync(command.BatchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            log.LogWarning("Batch {BatchId} not found.", command.BatchId);
            return;
        }

        var fromStatus = shipment.Status;
        var decision = await routing.SelectCarrierAsync(shipment, cancellationToken).ConfigureAwait(false);
        if (decision is null)
        {
            shipment.Fail("no eligible carrier", clock.UtcNow);
            await shipments.AddLineageAsync(
                ShipmentLineage.Record(shipment.Id, fromStatus, shipment.Status, "routing-engine", clock.UtcNow,
                    reason: "no eligible carrier"),
                cancellationToken).ConfigureAwait(false);
            batch.RecordItemFailed(shipment.Id, "no eligible carrier", clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        shipment.SelectCarrier(decision.CarrierCode, clock.UtcNow);
        shipment.MarkLabelRequested(clock.UtcNow);
        await shipments.AddLineageAsync(
            ShipmentLineage.Record(shipment.Id, fromStatus, shipment.Status, "routing-engine", clock.UtcNow,
                ruleAttribution: string.Join("; ", decision.AppliedRuleAttributions)),
            cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var carrierRegistration = registry.Get(decision.CarrierCode);
        var carrier = (ICarrierConnector)carrierRegistration.ConnectorFactory(serviceProvider);

        var labelRequest = new LabelCreationRequest(
            shipment.Id, shipment.From, shipment.To, shipment.Parcel, shipment.PreferredService, Reference: null);

        LabelCreationResult labelResult;
        try
        {
            labelResult = await carrier.CreateLabelAsync(labelRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Carrier {Carrier} threw on CreateLabelAsync for shipment {Shipment}.",
                decision.CarrierCode, shipment.Id);
            labelResult = new LabelCreationResult(false, null, null, null, ex.Message);
        }

        var beforeLabelStatus = shipment.Status;
        var scheduleTrackingPoll = false;
        if (labelResult.Success && labelResult.TrackingNumber is not null && labelResult.LabelUri is not null)
        {
            shipment.RecordLabel(labelResult.TrackingNumber, labelResult.LabelUri, clock.UtcNow);
            await shipments.AddLineageAsync(
                ShipmentLineage.Record(shipment.Id, beforeLabelStatus, shipment.Status, $"carrier:{decision.CarrierCode}",
                    clock.UtcNow, reason: "label created"),
                cancellationToken).ConfigureAwait(false);
            batch.RecordItemSucceeded(shipment.Id, clock.UtcNow);
            scheduleTrackingPoll = true;
        }
        else
        {
            var reason = labelResult.FailureReason ?? "carrier label failed";
            shipment.Fail(reason, clock.UtcNow);
            await shipments.AddLineageAsync(
                ShipmentLineage.Record(shipment.Id, beforeLabelStatus, shipment.Status, $"carrier:{decision.CarrierCode}",
                    clock.UtcNow, reason: reason),
                cancellationToken).ConfigureAwait(false);
            batch.RecordItemFailed(shipment.Id, reason, clock.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Concurrent peers may have just resolved their own items. Spin up a fresh DI scope
        // (= fresh DbContext, no cached entities) so loading the batch sees their committed
        // updates. The very last writer raises ShipmentBatchCompleted; earlier writers see
        // Pending items and exit via the idempotent guard inside RecheckCompletion.
        using (var rescope = serviceProvider.CreateScope())
        {
            var freshBatches = rescope.ServiceProvider.GetRequiredService<IShipmentBatchRepository>();
            var freshUow = rescope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var freshBatch = await freshBatches.FindAsync(command.BatchId, cancellationToken).ConfigureAwait(false);
            if (freshBatch is not null)
            {
                freshBatch.RecheckCompletion(clock.UtcNow);
                await freshUow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        // Kick off the first tracking poll. The carrier needs a moment to register the
        // parcel internally before we ask for events; in production we'd schedule a delay,
        // but Wolverine's scheduled messages add seconds of polling-loop latency that the
        // in-memory PostNL stub doesn't actually need. Publish immediately and let the
        // handler self-reschedule for subsequent attempts (which DO use ScheduleAsync).
        if (scheduleTrackingPoll)
        {
            await bus.PublishAsync(new PollShipmentTrackingCommand(shipment.Id, Attempt: 1))
                .ConfigureAwait(false);
        }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "RequestCarrierLabel for shipment {Shipment} failed.", command.ShipmentId);
            throw;
        }
    }
}

public sealed class RequestCarrierLabelMarker;
