using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Application.Ingestion;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Domain.ValueObjects;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;

namespace ShippingOrchestrator.PrivateApi.Endpoints;

/// <summary>
/// Operator-facing simulator: posts to <c>/admin/tenants/{tenantId}/simulate-order</c> dispatch
/// a synthetic <see cref="EcommerceOrderPayload"/> through the same ingest path a real Shopify
/// webhook uses. Intended for local end-to-end smoke testing and the test-parcel button on the
/// dashboard. Gated by <c>Simulators:Enabled</c> (true in Development by default; false in
/// Production) — the endpoint returns 404 when disabled.
/// </summary>
public static class SimulatorEndpoints
{
    public static void MapSimulatorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/tenants/{tenantId:guid}").WithTags("Admin: Simulator");

        group.MapPost("/simulate-order", async (
            Guid tenantId,
            SimulateOrderHttpRequest? request,
            IConfiguration config,
            ITenantRepository tenants,
            IEcommerceConnectionRepository connections,
            IIngestionDispatcher dispatcher,
            CancellationToken ct) =>
        {
            if (!config.GetValue("Simulators:Enabled", false))
                return Results.NotFound();

            var typedTenant = new TenantId(tenantId);
            var tenant = await tenants.FindAsync(typedTenant, ct).ConfigureAwait(false);
            if (tenant is null) return Results.NotFound(new { error = $"Tenant {tenantId} not found." });

            // Resolve a connection so the simulated order looks like the production path
            // (a real webhook would come from a known shop). Falls back to a synthetic
            // connector code if none is installed yet.
            var connectionList = await connections.ListForTenantAsync(typedTenant, ct).ConfigureAwait(false);
            var connection = connectionList.Count == 0 ? null : connectionList[0];
            var connectorCode = connection?.PlatformCode ?? "shopify";

            var payload = SimulatedOrderFactory.Build(typedTenant, connectorCode, request);
            var result = await dispatcher.DispatchAsync(payload, ct).ConfigureAwait(false);

            return Results.Accepted(
                $"/v1/dashboard/orders/pending/{result.PendingOrderId}",
                new SimulateOrderResponse(result.PendingOrderId, result.AlreadyPending, payload.ExternalOrderId));
        }).RequireAuthorization("Staff");
    }
}

public sealed record SimulateOrderHttpRequest(
    string? OriginCountry,
    string? DestinationCountry,
    int? WeightGrams,
    string? Description);

public sealed record SimulateOrderResponse(Guid PendingOrderId, bool AlreadyPending, string ExternalOrderId);

internal static class SimulatedOrderFactory
{
    public static EcommerceOrderPayload Build(TenantId tenantId, string connectorCode, SimulateOrderHttpRequest? overrides)
    {
        var origin = overrides?.OriginCountry ?? "NL";
        var dest = overrides?.DestinationCountry ?? "NL";
        var grams = overrides?.WeightGrams ?? 1000;

        var orderId = $"SIM-{Guid.NewGuid():N}"[..16].ToUpperInvariant();

        var fromAddress = new Address(
            Name: "Sample Warehouse",
            Line1: "Hoofdstraat 1",
            Line2: null,
            City: "Hoofddorp",
            Region: null,
            PostalCode: "2132 AA",
            Country: new CountryCode(origin));

        var toAddress = new Address(
            Name: "Sample Customer",
            Line1: "Damrak 70",
            Line2: null,
            City: "Amsterdam",
            Region: null,
            PostalCode: "1012 LM",
            Country: new CountryCode(dest));

        var unitPrice = new Money(24.99m, "EUR");
        var item = new EcommerceOrderLineItem(
            Sku: "ACME-WIDGET",
            Title: overrides?.Description ?? "Acme widget",
            Quantity: 1,
            UnitPrice: unitPrice,
            UnitWeight: Weight.FromGrams(grams));

        return new EcommerceOrderPayload(
            TenantId: tenantId,
            ConnectorCode: connectorCode,
            ExternalOrderId: orderId,
            Currency: "EUR",
            From: fromAddress,
            To: toAddress,
            Items: [item],
            TotalWeight: Weight.FromGrams(grams),
            PackageDimensions: new Dimension(LengthMm: 250, WidthMm: 200, HeightMm: 100),
            Reference: orderId,
            Description: item.Title,
            PreferredServiceCode: "STANDARD");
    }
}
