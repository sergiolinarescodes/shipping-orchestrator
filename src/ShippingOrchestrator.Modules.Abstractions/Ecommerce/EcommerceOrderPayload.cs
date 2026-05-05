using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Domain.ValueObjects;

namespace ShippingOrchestrator.Modules.Abstractions.Ecommerce;

/// <summary>
/// Connector-agnostic representation of "an ecommerce order that needs a shipment". Both real
/// webhooks (Shopify, future WooCommerce) and the admin simulator hand one of these to the
/// ingestion handler so the downstream pipeline sees a single shape regardless of source.
/// </summary>
public sealed record EcommerceOrderPayload(
    TenantId TenantId,
    string ConnectorCode,
    string ExternalOrderId,
    string Currency,
    Address From,
    Address To,
    IReadOnlyList<EcommerceOrderLineItem> Items,
    Weight TotalWeight,
    Dimension PackageDimensions,
    string? HintedCarrierCode = null,
    string? PreferredServiceCode = null,
    string? Reference = null,
    string? Description = null);

public sealed record EcommerceOrderLineItem(
    string Sku,
    string Title,
    int Quantity,
    Money UnitPrice,
    Weight UnitWeight,
    string? ExternalProductId = null);
