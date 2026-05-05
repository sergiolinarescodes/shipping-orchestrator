using ShippingOrchestrator.Application.Shipments;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.ReadModels.Abstractions.Customer;
using Wolverine;

namespace ShippingOrchestrator.PublicApi.Endpoints;

public static class ShipmentEndpoints
{
    public static void MapShipmentEndpoints(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/v1/shipments").WithTags("Shipments");

        v1.MapPost("/batches", async (
            CreateBatchHttpRequest request,
            HttpRequest http,
            IMessageBus bus,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var tenantId = tenantContext.Current
                ?? throw new InvalidOperationException("Tenant context is not set on this request.");

            var idempotencyKey = http.Headers.TryGetValue("Idempotency-Key", out var values)
                ? values.ToString()
                : null;

            var command = new CreateShipmentBatchCommand(tenantId, idempotencyKey, request.Shipments);
            // Handler returns (CreateShipmentBatchResult, ProcessShipmentBatchCommand?). Wolverine
            // treats the second tuple member as a cascading message published automatically; the
            // caller asks only for the result type.
            var result = await bus.InvokeAsync<CreateShipmentBatchResult>(command, ct).ConfigureAwait(false);

            return Results.Accepted(
                $"/v1/shipments/batches/{result.BatchId}",
                new CreateShipmentBatchResponse(
                    result.BatchId,
                    $"/v1/shipments/batches/{result.BatchId}",
                    result.ShipmentIds));
        }).RequireAuthorization("Tenant").WithName("CreateShipmentBatch");

        v1.MapGet("/batches/{batchId:guid}", async (
            Guid batchId,
            ICustomerReadQueries queries,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var tenantId = tenantContext.Current
                ?? throw new InvalidOperationException("Tenant context is not set on this request.");
            var view = await queries.GetBatchAsync(tenantId, batchId, ct).ConfigureAwait(false);
            return view is null ? Results.NotFound() : Results.Ok(view);
        }).RequireAuthorization("Tenant").WithName("GetShipmentBatch");

        v1.MapGet("/{shipmentId:guid}", async (
            Guid shipmentId,
            ICustomerReadQueries queries,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var tenantId = tenantContext.Current
                ?? throw new InvalidOperationException("Tenant context is not set on this request.");
            var view = await queries.GetShipmentAsync(tenantId, shipmentId, ct).ConfigureAwait(false);
            return view is null ? Results.NotFound() : Results.Ok(view);
        }).RequireAuthorization("Tenant").WithName("GetShipment");
    }
}

public sealed record CreateBatchHttpRequest(IReadOnlyList<ShipmentRequestDto> Shipments);

public sealed record CreateShipmentBatchResponse(
    Guid BatchId,
    string StatusUrl,
    IReadOnlyList<Guid> ShipmentIds);
