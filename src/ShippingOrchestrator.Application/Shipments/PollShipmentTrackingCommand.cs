using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Shipments;
using ShippingOrchestrator.Modules.Abstractions;
using ShippingOrchestrator.Modules.Abstractions.Carriers;
using Wolverine;

namespace ShippingOrchestrator.Application.Shipments;

/// <summary>
/// Wolverine-scheduled message that polls the shipment's carrier for tracking events and
/// folds them into the aggregate. Re-schedules itself with backoff until the shipment reaches
/// a terminal state or <see cref="ShipmentTrackingPollOptions.MaxAttempts"/> is reached. The
/// in-memory PostNL connector returns deterministic events so locally the timeline appears
/// within ~10 s of label creation.
/// </summary>
public sealed record PollShipmentTrackingCommand(Guid ShipmentId, int Attempt = 1);

public sealed class ShipmentTrackingPollOptions
{
    public const string SectionName = "ShipmentTracking";
    public int MaxAttempts { get; set; } = 6;
    public int IntervalSeconds { get; set; } = 5;
}

public static class PollShipmentTrackingHandler
{
    public static async Task Handle(
        PollShipmentTrackingCommand command,
        IShipmentRepository shipments,
        ConnectorRegistry registry,
        IServiceProvider services,
        IUnitOfWork unitOfWork,
        IClock clock,
        IMessageBus bus,
        IOptions<ShipmentTrackingPollOptions> options,
        ILogger<PollShipmentTrackingMarker> log,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var shipment = await shipments.FindAsync(command.ShipmentId, cancellationToken).ConfigureAwait(false);
        if (shipment is null) return;
        if (shipment.Status is ShipmentStatus.Delivered or ShipmentStatus.Cancelled or ShipmentStatus.Failed) return;
        if (string.IsNullOrWhiteSpace(shipment.CarrierCode) || string.IsNullOrWhiteSpace(shipment.TrackingNumber)) return;

        if (!registry.TryGet(shipment.CarrierCode, out var registration) || registration is null)
        {
            log.LogWarning("Carrier '{Carrier}' not registered while polling shipment {Shipment}.",
                shipment.CarrierCode, shipment.Id);
            return;
        }

        try
        {
            var carrier = (ICarrierConnector)registration.ConnectorFactory(services);
            var result = await carrier.TrackAsync(shipment.TrackingNumber, cancellationToken).ConfigureAwait(false);
            if (result.Found && result.Events.Count > 0)
            {
                var updates = result.Events
                    .Select(e => new ShipmentTrackingUpdate(e.Status, e.Description, e.Location, e.OccurredAt))
                    .ToArray();
                shipment.AppendTrackingEvents(updates, clock.UtcNow);
                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Tracking poll for shipment {Shipment} (attempt {Attempt}) failed.",
                command.ShipmentId, command.Attempt);
        }

        // Re-fetch to consult the latest status (may have been promoted to InTransit / Delivered).
        var refreshed = await shipments.FindAsync(command.ShipmentId, cancellationToken).ConfigureAwait(false);
        var terminal = refreshed?.Status is ShipmentStatus.Delivered or ShipmentStatus.Cancelled or ShipmentStatus.Failed;
        if (!terminal && command.Attempt < settings.MaxAttempts)
        {
            await bus.ScheduleAsync(
                new PollShipmentTrackingCommand(command.ShipmentId, command.Attempt + 1),
                TimeSpan.FromSeconds(settings.IntervalSeconds)).ConfigureAwait(false);
        }
    }
}

public sealed class PollShipmentTrackingMarker;
