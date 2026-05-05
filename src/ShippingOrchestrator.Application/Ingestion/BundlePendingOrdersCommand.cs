using System.Text.Json;
using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Application.Shipments;
using ShippingOrchestrator.Domain.Ingestion;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Domain.ValueObjects;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;
using Wolverine;

namespace ShippingOrchestrator.Application.Ingestion;

/// <summary>
/// Customer-driven action that bundles several pending ecommerce orders into a single shipment
/// batch. Loads each <see cref="PendingEcommerceOrder"/>, deserializes the stored payload back
/// into <see cref="EcommerceOrderPayload"/>, builds one <see cref="CreateShipmentBatchCommand"/>
/// containing one shipment per pending order, and on success marks each pending row consumed
/// against the resulting batch id.
/// </summary>
public sealed record BundlePendingOrdersCommand(
    TenantId TenantId,
    IReadOnlyList<Guid> PendingOrderIds,
    string? IdempotencyKey);

public sealed record BundlePendingOrdersResult(
    Guid BatchId,
    IReadOnlyList<Guid> ShipmentIds,
    IReadOnlyList<Guid> ConsumedPendingOrderIds);

public static class BundlePendingOrdersHandler
{
    public static async Task<BundlePendingOrdersResult> Handle(
        BundlePendingOrdersCommand command,
        IPendingEcommerceOrderRepository pendingRepo,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        if (command.PendingOrderIds.Count == 0)
            throw new ArgumentException("At least one pending order id is required.", nameof(command));

        var pendings = await pendingRepo
            .LoadManyAsync(command.TenantId, command.PendingOrderIds, cancellationToken)
            .ConfigureAwait(false);
        if (pendings.Count != command.PendingOrderIds.Count)
            throw new InvalidOperationException(
                $"Expected {command.PendingOrderIds.Count} pending orders, found {pendings.Count}. " +
                "Some ids do not belong to this tenant or have already been consumed.");

        var unconsumed = pendings.Where(p => !p.IsConsumed).ToArray();
        if (unconsumed.Length == 0)
            throw new InvalidOperationException("All selected orders are already consumed.");

        var shipmentDtos = unconsumed.Select(BuildShipmentRequest).ToArray();
        var idempotencyKey = command.IdempotencyKey ?? $"bundle:{command.TenantId}:{string.Join(",", unconsumed.Select(p => p.Id).OrderBy(g => g))}";

        var batchResult = await bus.InvokeAsync<CreateShipmentBatchResult>(
            new CreateShipmentBatchCommand(command.TenantId, idempotencyKey, shipmentDtos),
            cancellationToken).ConfigureAwait(false);

        var consumedIds = unconsumed.Select(p => p.Id).ToArray();
        await pendingRepo
            .MarkConsumedAsync(consumedIds, batchResult.BatchId, DateTimeOffset.UtcNow, cancellationToken)
            .ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new BundlePendingOrdersResult(batchResult.BatchId, batchResult.ShipmentIds, consumedIds);
    }

    private static ShipmentRequestDto BuildShipmentRequest(PendingEcommerceOrder pending)
    {
        var payload = JsonSerializer.Deserialize<EcommerceOrderPayload>(pending.PayloadJson)
            ?? throw new InvalidOperationException(
                $"Pending order {pending.Id} has unparseable payload JSON.");

        var parcel = new Parcel(
            Weight: payload.TotalWeight.Grams > 0 ? payload.TotalWeight : Weight.FromGrams(500),
            Dimensions: payload.PackageDimensions,
            DeclaredValue: ComputeDeclaredValue(payload),
            Reference: payload.Reference ?? payload.ExternalOrderId,
            Description: payload.Description ?? FirstItemTitle(payload));

        return new ShipmentRequestDto(
            From: payload.From,
            To: payload.To,
            Parcel: parcel,
            PreferredServiceCode: payload.PreferredServiceCode);
    }

    private static Money ComputeDeclaredValue(EcommerceOrderPayload payload)
    {
        if (payload.Items.Count == 0)
            return new Money(0m, payload.Currency);
        var total = payload.Items.Aggregate(0m, (sum, i) => sum + i.UnitPrice.Amount * i.Quantity);
        var currency = payload.Items[0].UnitPrice.Currency;
        return new Money(total, currency);
    }

    private static string? FirstItemTitle(EcommerceOrderPayload payload) =>
        payload.Items.Count == 0 ? null : payload.Items[0].Title;
}
