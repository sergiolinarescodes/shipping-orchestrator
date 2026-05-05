using System.Text.Json;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Domain.ValueObjects;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;

namespace ShippingOrchestrator.EcommerceConnectors.Shopify;

/// <summary>
/// Maps Shopify Admin API order JSON (the body Shopify pushes on
/// <c>orders/create</c> / <c>orders/fulfilled</c>) into the
/// connector-agnostic <see cref="EcommerceOrderPayload"/>. Anti-corruption layer for the
/// rest of the system: nothing outside this project sees Shopify's per-line-item shape,
/// shipping_address.country_code casing quirks, or grams-as-string weights.
/// </summary>
public sealed class ShopifyOrderTranslator : IEcommerceOrderTranslator
{
    public string ConnectorCode => "shopify";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public Task<EcommerceOrderPayload> TranslateAsync(
        TenantId tenantId,
        string externalAccountId,
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawBody);
        var dto = JsonSerializer.Deserialize<ShopifyOrderDto>(rawBody, JsonOptions)
            ?? throw new InvalidOperationException("Shopify order body could not be parsed.");

        var currency = dto.Currency ?? "EUR";
        var externalOrderId = dto.Id?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? dto.Name;
        var shipping = dto.ShippingAddress ?? dto.BillingAddress
            ?? throw IngestionTranslationException.Missing(
                ConnectorCode,
                externalOrderId,
                "shipping or billing address",
                "Add a shipping or billing address in your Shopify admin (Orders → this order → Edit). " +
                "Then click Recheck here, or save the order to fire an orders/updated webhook.");

        var origin = new Address(
            Name: dto.Shop?.Name ?? externalAccountId,
            Line1: "Origin Address Line 1",
            Line2: null,
            City: "Origin City",
            Region: null,
            PostalCode: "0000 AA",
            Country: new CountryCode(dto.Shop?.CountryCode ?? "NL"));

        var destination = new Address(
            Name: $"{shipping.FirstName} {shipping.LastName}".Trim(),
            Line1: shipping.Address1 ?? string.Empty,
            Line2: shipping.Address2,
            City: shipping.City ?? string.Empty,
            Region: shipping.Province,
            PostalCode: shipping.Zip ?? string.Empty,
            Country: new CountryCode(shipping.CountryCode ?? "NL"),
            Phone: shipping.Phone,
            Email: dto.Email);

        var items = (dto.LineItems ?? [])
            .Select(li =>
            {
                var unitGrams = li.Grams ?? 0;
                var unitPrice = decimal.TryParse(li.Price, System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : 0m;
                return new EcommerceOrderLineItem(
                    Sku: li.Sku ?? string.Empty,
                    Title: li.Title ?? string.Empty,
                    Quantity: li.Quantity ?? 1,
                    UnitPrice: new Money(unitPrice, currency),
                    UnitWeight: Weight.FromGrams(unitGrams));
            })
            .ToArray();

        var totalGrams = items.Sum(i => i.UnitWeight.Grams * i.Quantity);
        var totalWeight = totalGrams > 0 ? Weight.FromGrams(totalGrams) : Weight.FromGrams(500);
        var dimensions = new Dimension(LengthMm: 250, WidthMm: 200, HeightMm: 100);

        return Task.FromResult(new EcommerceOrderPayload(
            TenantId: tenantId,
            ConnectorCode: ConnectorCode,
            ExternalOrderId: externalOrderId ?? Guid.NewGuid().ToString(),
            Currency: currency,
            From: origin,
            To: destination,
            Items: items,
            TotalWeight: totalWeight,
            PackageDimensions: dimensions,
            HintedCarrierCode: null,
            PreferredServiceCode: null,
            Reference: dto.Name,
            Description: items.FirstOrDefault()?.Title));
    }

    private sealed record ShopifyOrderDto(
        long? Id,
        string? Name,
        string? Email,
        string? Currency,
        ShopifyShopDto? Shop,
        ShopifyAddressDto? ShippingAddress,
        ShopifyAddressDto? BillingAddress,
        IReadOnlyList<ShopifyLineItemDto>? LineItems);

    private sealed record ShopifyShopDto(string? Name, string? CountryCode);

    private sealed record ShopifyAddressDto(
        string? FirstName,
        string? LastName,
        string? Address1,
        string? Address2,
        string? City,
        string? Province,
        string? Zip,
        string? CountryCode,
        string? Phone);

    private sealed record ShopifyLineItemDto(
        string? Sku,
        string? Title,
        int? Quantity,
        int? Grams,
        string? Price);
}
