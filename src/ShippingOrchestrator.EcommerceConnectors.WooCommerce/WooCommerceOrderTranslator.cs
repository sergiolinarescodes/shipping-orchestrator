using System.Globalization;
using System.Text.Json;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Domain.ValueObjects;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;

namespace ShippingOrchestrator.EcommerceConnectors.WooCommerce;

/// <summary>
/// Maps a WooCommerce REST <c>order.created</c>/<c>order.updated</c> webhook body into the
/// connector-agnostic <see cref="EcommerceOrderPayload"/>. Anti-corruption layer for the rest
/// of the system: nothing outside this project sees WC's snake_case keys, the per-line-item
/// weight string, or the empty-shipping-falls-back-to-billing convention.
/// </summary>
public sealed class WooCommerceOrderTranslator : IEcommerceOrderTranslator
{
    public string ConnectorCode => "woocommerce";

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
        var dto = JsonSerializer.Deserialize<WooOrderDto>(rawBody, JsonOptions)
            ?? throw new InvalidOperationException("WooCommerce order body could not be parsed.");

        var currency = string.IsNullOrWhiteSpace(dto.Currency) ? "EUR" : dto.Currency;
        var externalOrderId = dto.Id?.ToString(CultureInfo.InvariantCulture) ?? dto.Number;

        // WooCommerce always sends both billing + shipping objects but `shipping` is empty when
        // the merchant hasn't enabled shipping addresses or the customer reused billing.
        var shipping = HasUsableAddress(dto.Shipping) ? dto.Shipping : dto.Billing;
        if (shipping is null || !HasUsableAddress(shipping))
            throw IngestionTranslationException.Missing(
                ConnectorCode,
                externalOrderId,
                "shipping or billing address",
                "Add a shipping or billing address to this order in your WooCommerce admin (Orders → this order → Edit). " +
                "Then click Recheck here, or save the order in admin to re-trigger the order.updated webhook.");

        var origin = new Address(
            Name: externalAccountId,
            Line1: "Origin Address Line 1",
            Line2: null,
            City: "Origin City",
            Region: null,
            PostalCode: "0000 AA",
            Country: new CountryCode("NL"));

        if (string.IsNullOrWhiteSpace(shipping!.Country))
            throw new IngestionTranslationException(
                IngestionReasonCode.UnknownCountry,
                ConnectorCode,
                externalOrderId,
                "Set a destination country on this order in your WooCommerce admin using the ISO-2 code (NL, DE, FR, …). " +
                "Then click Recheck here to re-pull the order.",
                "WooCommerce order is missing the destination country.");

        var destination = new Address(
            Name: $"{shipping.FirstName} {shipping.LastName}".Trim(),
            Line1: shipping.Address1 ?? string.Empty,
            Line2: shipping.Address2,
            City: shipping.City ?? string.Empty,
            Region: shipping.State,
            PostalCode: shipping.Postcode ?? string.Empty,
            Country: new CountryCode(shipping.Country),
            Phone: dto.Billing?.Phone,
            Email: dto.Billing?.Email);

        var items = (dto.LineItems ?? [])
            .Select(li =>
            {
                var priceText = li.Price switch
                {
                    JsonElement el => el.ValueKind == JsonValueKind.String ? el.GetString() : el.GetRawText(),
                    null => null,
                    _ => Convert.ToString(li.Price, CultureInfo.InvariantCulture),
                };
                var unitPrice = decimal.TryParse(priceText, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : 0m;
                var quantity = li.Quantity ?? 1;
                // Stock WC's REST order shape doesn't carry per-line product weight — that lives
                // on the product, not the order. Capture the product id (or variation id when set)
                // so WooCommerceEcommerceConnector.EnrichAsync can fill the weight in afterward.
                var unitGrams = ParseGrams(li.MetaData);
                var productRef = (li.VariationId is > 0 ? li.VariationId : li.ProductId)?
                    .ToString(CultureInfo.InvariantCulture);
                return new EcommerceOrderLineItem(
                    Sku: li.Sku ?? string.Empty,
                    Title: li.Name ?? string.Empty,
                    Quantity: quantity,
                    UnitPrice: new Money(unitPrice, currency),
                    UnitWeight: Weight.FromGrams(unitGrams),
                    ExternalProductId: productRef);
            })
            .ToArray();

        // Zero-weight enforcement is intentionally NOT done here — the WC enricher fetches
        // products from the REST API after translation to fill in missing weights. The
        // dispatch caller validates the final total once enrichment has run.
        var totalGrams = items.Sum(i => i.UnitWeight.Grams * i.Quantity);
        var totalWeight = Weight.FromGrams(totalGrams);
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
            Reference: dto.Number is null ? null : $"#{dto.Number}",
            Description: items.FirstOrDefault()?.Title));
    }

    private static bool HasUsableAddress(WooAddressDto? addr) =>
        addr is not null
        && (!string.IsNullOrWhiteSpace(addr.Address1) || !string.IsNullOrWhiteSpace(addr.City) || !string.IsNullOrWhiteSpace(addr.Postcode));

    /// <summary>WC stores per-line item weight under <c>meta_data</c> with key <c>_weight</c> (grams).</summary>
    private static int ParseGrams(IReadOnlyList<WooMetaDto>? meta)
    {
        if (meta is null) return 0;
        foreach (var m in meta)
        {
            if (string.Equals(m.Key, "_weight", StringComparison.OrdinalIgnoreCase) && m.Value is not null
                && decimal.TryParse(m.Value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var kg))
            {
                // WC stores weight in the configured unit (kg by default for European stores).
                return (int)Math.Round(kg * 1000m);
            }
        }
        return 0;
    }

    private sealed record WooOrderDto(
        long? Id,
        string? Number,
        string? Currency,
        WooAddressDto? Billing,
        WooAddressDto? Shipping,
        IReadOnlyList<WooLineItemDto>? LineItems);

    private sealed record WooAddressDto(
        string? FirstName,
        string? LastName,
        string? Company,
        string? Address1,
        string? Address2,
        string? City,
        string? State,
        string? Postcode,
        string? Country,
        string? Email,
        string? Phone);

    private sealed record WooLineItemDto(
        long? Id,
        string? Name,
        string? Sku,
        int? Quantity,
        object? Price,
        long? ProductId,
        long? VariationId,
        IReadOnlyList<WooMetaDto>? MetaData);

    private sealed record WooMetaDto(string? Key, object? Value);
}
