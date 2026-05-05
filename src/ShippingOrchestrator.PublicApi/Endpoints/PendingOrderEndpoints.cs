using System.Text.Json;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Application.Ingestion;
using ShippingOrchestrator.Domain.Ingestion;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;
using Wolverine;

namespace ShippingOrchestrator.PublicApi.Endpoints;

/// <summary>
/// Tenant-facing inbox of ecommerce orders received via webhook (or simulator) but not yet
/// bundled into a shipment batch. The customer dashboard lists pending rows and bundles a
/// selection into a single batch with one click.
/// </summary>
public static class PendingOrderEndpoints
{
    public static void MapPendingOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/dashboard/orders").WithTags("Dashboard (Customer)");

        group.MapGet("/pending", async (
            int? take,
            IPendingEcommerceOrderRepository repo,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var tenantId = tenantContext.Current
                ?? throw new InvalidOperationException("Tenant context is not set on this request.");
            var rows = await repo.ListPendingForTenantAsync(tenantId, take ?? 100, ct).ConfigureAwait(false);
            var views = rows.Select(PendingOrderView.From).ToArray();
            return Results.Ok(views);
        }).RequireAuthorization("Tenant").WithName("ListPendingOrders");

        group.MapPost("/bundle", async (
            BundlePendingOrdersHttpRequest request,
            IMessageBus bus,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var tenantId = tenantContext.Current
                ?? throw new InvalidOperationException("Tenant context is not set on this request.");
            if (request.OrderIds is null || request.OrderIds.Count == 0)
                return Results.BadRequest(new { error = "orderIds must not be empty." });

            var result = await bus.InvokeAsync<BundlePendingOrdersResult>(
                new BundlePendingOrdersCommand(tenantId, request.OrderIds, request.IdempotencyKey),
                ct).ConfigureAwait(false);

            return Results.Accepted(
                $"/v1/shipments/batches/{result.BatchId}",
                new BundlePendingOrdersResponse(result.BatchId, result.ShipmentIds, result.ConsumedPendingOrderIds));
        }).RequireAuthorization("Tenant").WithName("BundlePendingOrders");
    }
}

public sealed record BundlePendingOrdersHttpRequest(IReadOnlyList<Guid> OrderIds, string? IdempotencyKey);

public sealed record BundlePendingOrdersResponse(
    Guid BatchId,
    IReadOnlyList<Guid> ShipmentIds,
    IReadOnlyList<Guid> ConsumedPendingOrderIds);

public sealed record PendingOrderView(
    Guid Id,
    string PlatformCode,
    string ExternalOrderId,
    DateTimeOffset IngestedAt,
    string? CustomerName,
    string? DestinationCity,
    string? DestinationCountry,
    int ItemCount,
    int TotalWeightGrams,
    decimal? DeclaredValue,
    string? Currency)
{
    internal static PendingOrderView From(PendingEcommerceOrder order)
    {
        EcommerceOrderPayload? payload = null;
        try
        {
            payload = JsonSerializer.Deserialize<EcommerceOrderPayload>(order.PayloadJson);
        }
        catch (JsonException)
        {
            // Fall through with null payload; view shows minimum data.
        }

        if (payload is null)
            return new PendingOrderView(
                order.Id, order.PlatformCode, order.ExternalOrderId, order.IngestedAt,
                CustomerName: null, DestinationCity: null, DestinationCountry: null,
                ItemCount: 0, TotalWeightGrams: 0, DeclaredValue: null, Currency: null);

        var declaredValue = payload.Items.Count == 0
            ? (decimal?)null
            : payload.Items.Sum(i => i.UnitPrice.Amount * i.Quantity);

        return new PendingOrderView(
            order.Id,
            order.PlatformCode,
            order.ExternalOrderId,
            order.IngestedAt,
            CustomerName: payload.To.Name,
            DestinationCity: payload.To.City,
            DestinationCountry: payload.To.Country.Value,
            ItemCount: payload.Items.Sum(i => i.Quantity),
            TotalWeightGrams: payload.TotalWeight.Grams,
            DeclaredValue: declaredValue,
            Currency: payload.Currency);
    }
}
