using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Shipments;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Domain.ValueObjects;

namespace ShippingOrchestrator.Application.Shipments;

public sealed record CreateShipmentBatchCommand(
    TenantId TenantId,
    string? IdempotencyKey,
    IReadOnlyList<ShipmentRequestDto> Shipments);

public sealed record ShipmentRequestDto(
    Address From,
    Address To,
    Parcel Parcel,
    string? PreferredServiceCode);

public sealed record CreateShipmentBatchResult(Guid BatchId, IReadOnlyList<Guid> ShipmentIds);

public static class CreateShipmentBatchHandler
{
    public static async Task<(CreateShipmentBatchResult, ProcessShipmentBatchCommand?)> Handle(
        CreateShipmentBatchCommand command,
        IShipmentRepository shipments,
        IShipmentBatchRepository batches,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (command.Shipments.Count == 0)
            throw new ArgumentException("Batch must contain at least one shipment.", nameof(command));

        var idempotencyKey = command.IdempotencyKey is null
            ? (IdempotencyKey?)null
            : ValueObjects.IdempotencyKey.Parse(command.IdempotencyKey);

        if (idempotencyKey is { } key)
        {
            var existing = await batches
                .FindByIdempotencyKeyAsync(command.TenantId, key, cancellationToken)
                .ConfigureAwait(false);
            if (existing is not null)
            {
                var existingResult = new CreateShipmentBatchResult(
                    existing.Id,
                    existing.Items.Select(i => i.ShipmentId).ToArray());
                // Cascade returns the result without re-publishing the process command (already done).
                return (existingResult, null);
            }
        }

        var batchId = Guid.NewGuid();
        var shipmentIds = new List<Guid>(command.Shipments.Count);
        var now = clock.UtcNow;
        foreach (var dto in command.Shipments)
        {
            var preferredService = string.IsNullOrWhiteSpace(dto.PreferredServiceCode)
                ? (ServiceLevel?)null
                : new ServiceLevel(dto.PreferredServiceCode!.ToUpperInvariant());

            // Each shipment is born already linked to its batch so the read-side projection
            // can index shipments by BatchId from the very first ShipmentCreated event.
            var shipment = Shipment.Create(command.TenantId, batchId, dto.From, dto.To, dto.Parcel, preferredService, now);
            await shipments.AddAsync(shipment, cancellationToken).ConfigureAwait(false);
            shipmentIds.Add(shipment.Id);
        }

        var batch = ShipmentBatch.AcceptWithId(batchId, command.TenantId, idempotencyKey, shipmentIds, now);
        await batches.AddAsync(batch, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (new CreateShipmentBatchResult(batch.Id, shipmentIds), new ProcessShipmentBatchCommand(batch.Id));
    }

    private static class ValueObjects
    {
        internal static class IdempotencyKey
        {
            internal static Domain.ValueObjects.IdempotencyKey Parse(string value) =>
                Domain.ValueObjects.IdempotencyKey.Parse(value);
        }
    }
}

/// <summary>
/// Worker-side command kicked off after a batch has been persisted. Wolverine's outbox
/// guarantees this is only delivered after the producing transaction commits.
/// </summary>
public sealed record ProcessShipmentBatchCommand(Guid BatchId);
