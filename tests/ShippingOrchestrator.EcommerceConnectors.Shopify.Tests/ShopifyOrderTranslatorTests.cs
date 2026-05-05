using FluentAssertions;
using NUnit.Framework;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.EcommerceConnectors.Shopify;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;

namespace ShippingOrchestrator.EcommerceConnectors.Shopify.Tests;

[TestFixture]
public class ShopifyOrderTranslatorTests
{
    private const string SampleBody = """
        {
          "id": 4509923842,
          "name": "#1042",
          "email": "buyer@example.com",
          "currency": "EUR",
          "shop": { "name": "Acme NL", "country_code": "NL" },
          "shipping_address": {
            "first_name": "Anne",
            "last_name": "de Vries",
            "address1": "Damrak 70",
            "address2": null,
            "city": "Amsterdam",
            "province": null,
            "zip": "1012 LM",
            "country_code": "NL",
            "phone": "+31201234567"
          },
          "line_items": [
            { "sku": "ACME-WIDGET", "title": "Acme widget", "quantity": 2, "grams": 250, "price": "12.50" }
          ]
        }
        """;

    [Test]
    public async Task Translates_shopify_payload_into_normalized_order()
    {
        var translator = new ShopifyOrderTranslator();
        var tenant = TenantId.New();
        var headers = new Dictionary<string, string> { ["X-Shopify-Topic"] = "orders/create" };

        var payload = await translator.TranslateAsync(tenant, "acme-nl.myshopify.com", SampleBody, headers, CancellationToken.None);

        payload.TenantId.Should().Be(tenant);
        payload.ConnectorCode.Should().Be("shopify");
        payload.ExternalOrderId.Should().Be("4509923842");
        payload.Currency.Should().Be("EUR");
        payload.From.Country.Value.Should().Be("NL");
        payload.To.City.Should().Be("Amsterdam");
        payload.To.PostalCode.Should().Be("1012 LM");
        payload.Items.Should().HaveCount(1);
        payload.Items[0].Sku.Should().Be("ACME-WIDGET");
        payload.Items[0].Quantity.Should().Be(2);
        payload.TotalWeight.Grams.Should().Be(500); // 250g * 2
        payload.Reference.Should().Be("#1042");
    }

    [Test]
    public async Task Falls_back_to_default_weight_when_grams_missing()
    {
        // PINS CURRENT BEHAVIOUR — Shopify returns a hard-coded 500g default when no line item
        // carries grams. This masks the same class of misconfiguration the WC path now surfaces
        // as ZeroWeight: a merchant with zero-weight products ships at 500g without anyone
        // noticing. Real Shopify webhooks DO include `grams` per line item, so the only path
        // that lands here is "merchant didn't set a product weight" — and that's exactly what
        // the dashboard's Recheck flow + IngestionFailure UX exists to surface. Flagged for
        // alignment with WC's post-enrichment ZeroWeight validation.
        var body = """
            {
              "id": 1,
              "currency": "EUR",
              "shipping_address": { "first_name": "X", "last_name": "Y", "address1": "Line", "city": "Amsterdam", "zip": "1012 AB", "country_code": "NL" },
              "line_items": [ { "sku": "S", "title": "T", "quantity": 1, "price": "10.00" } ]
            }
            """;

        var translator = new ShopifyOrderTranslator();
        var payload = await translator.TranslateAsync(TenantId.New(), "shop.myshopify.com", body, new Dictionary<string, string>(), CancellationToken.None);
        payload.TotalWeight.Grams.Should().Be(500);
    }

    [Test]
    public async Task Throws_typed_translation_exception_when_address_missing()
    {
        var body = """
            {
              "id": 4509923842,
              "name": "#1042",
              "currency": "EUR",
              "line_items": [ { "sku": "S", "title": "T", "quantity": 1, "price": "10.00" } ]
            }
            """;

        var translator = new ShopifyOrderTranslator();

        var act = async () => await translator.TranslateAsync(
            TenantId.New(), "acme-nl.myshopify.com", body, new Dictionary<string, string>(), CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<IngestionTranslationException>()).Which;
        ex.Code.Should().Be(IngestionReasonCode.MissingShippingAddress);
        ex.ConnectorCode.Should().Be("shopify");
        ex.ExternalOrderId.Should().Be("4509923842");
        ex.TenantHint.Should().Contain("Shopify");
    }
}
