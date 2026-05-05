using FluentAssertions;
using NUnit.Framework;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.EcommerceConnectors.WooCommerce;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;

namespace ShippingOrchestrator.EcommerceConnectors.WooCommerce.Tests;

[TestFixture]
public class WooCommerceOrderTranslatorTests
{
    // This sample mirrors the real shape WC's REST API ships for orders/created /
    // orders/updated: line items carry product_id + quantity but NO weight (weight lives on
    // the product). A passing fixture WITH a synthetic _weight meta would silently mask the
    // very bug that motivated the enricher — so this body intentionally has no _weight key.
    private const string RealisticWooBody = """
        {
          "id": 7421,
          "number": "7421",
          "currency": "EUR",
          "billing": {
            "first_name": "Sven",
            "last_name": "Janssen",
            "address_1": "Damrak 70",
            "city": "Amsterdam",
            "postcode": "1012 LM",
            "country": "NL",
            "email": "buyer@example.com",
            "phone": "+31201234567"
          },
          "shipping": {
            "first_name": "Sven",
            "last_name": "Janssen",
            "address_1": "Damrak 70",
            "city": "Amsterdam",
            "postcode": "1012 LM",
            "country": "NL"
          },
          "line_items": [
            {
              "id": 1,
              "name": "Acme widget",
              "sku": "ACME-WIDGET",
              "product_id": 42,
              "variation_id": 0,
              "quantity": 2,
              "price": "12.50",
              "meta_data": []
            }
          ]
        }
        """;

    [Test]
    public async Task Translates_real_woocommerce_payload_with_zero_weight_and_captured_product_id()
    {
        var translator = new WooCommerceOrderTranslator();
        var tenant = TenantId.New();
        var headers = new Dictionary<string, string> { ["X-WC-Webhook-Topic"] = "order.created" };

        var payload = await translator.TranslateAsync(tenant, "https://store.example.com", RealisticWooBody, headers, CancellationToken.None);

        payload.TenantId.Should().Be(tenant);
        payload.ConnectorCode.Should().Be("woocommerce");
        payload.ExternalOrderId.Should().Be("7421");
        payload.Currency.Should().Be("EUR");
        payload.To.City.Should().Be("Amsterdam");
        payload.To.Email.Should().Be("buyer@example.com");
        payload.Items.Should().HaveCount(1);
        payload.Items[0].Sku.Should().Be("ACME-WIDGET");
        payload.Items[0].Quantity.Should().Be(2);
        payload.Items[0].ExternalProductId.Should().Be("42",
            "the enricher relies on this id to fetch /products/{id} for the missing weight");
        payload.TotalWeight.Grams.Should().Be(0,
            "stock WC orders never carry weight inline — enricher fills it via REST");
        payload.Reference.Should().Be("#7421");
    }

    [Test]
    public async Task Honors_legacy_underscore_weight_meta_when_a_plugin_supplies_it()
    {
        // Some WC plugins / custom code surface product weight in line_items.meta_data[_weight].
        // We honor it so a tenant who already has that wired keeps working without a REST hop.
        // This is a fallback path, not the canonical one — see the realistic test above.
        var body = """
            {
              "id": 7000,
              "currency": "EUR",
              "billing": { "first_name": "X", "last_name": "Y", "address_1": "Line", "city": "Amsterdam", "postcode": "1012 AB", "country": "NL" },
              "shipping": { "first_name": "X", "last_name": "Y", "address_1": "Line", "city": "Amsterdam", "postcode": "1012 AB", "country": "NL" },
              "line_items": [ { "id": 1, "name": "T", "sku": "S", "product_id": 42, "quantity": 2, "price": "10.00",
                "meta_data": [{ "key": "_weight", "value": "0.250" }] } ]
            }
            """;

        var translator = new WooCommerceOrderTranslator();
        var payload = await translator.TranslateAsync(TenantId.New(), "https://shop.example", body, new Dictionary<string, string>(), CancellationToken.None);

        payload.Items[0].UnitWeight.Grams.Should().Be(250);
        payload.TotalWeight.Grams.Should().Be(500);
    }

    [Test]
    public async Task Falls_back_to_billing_when_shipping_is_empty()
    {
        var body = """
            {
              "id": 1,
              "currency": "EUR",
              "billing": { "first_name": "X", "last_name": "Y", "address_1": "Line", "city": "Amsterdam", "postcode": "1012 AB", "country": "NL" },
              "shipping": { "first_name": "", "last_name": "", "address_1": "", "city": "", "postcode": "", "country": "" },
              "line_items": [ { "id": 1, "name": "Thing", "sku": "S", "product_id": 1, "quantity": 1, "price": "10.00", "meta_data": [] } ]
            }
            """;

        var translator = new WooCommerceOrderTranslator();
        var payload = await translator.TranslateAsync(TenantId.New(), "https://shop.example", body, new Dictionary<string, string>(), CancellationToken.None);

        payload.To.City.Should().Be("Amsterdam",
            "billing address must back-fill when WC sends an empty shipping object");
    }

    [Test]
    public async Task Returns_zero_weight_payload_when_no_line_item_has_weight()
    {
        // Stock WC's REST order shape has no per-line weight — the translator captures
        // product_id and leaves weight at zero so WooCommerceEcommerceConnector.EnrichAsync can
        // fill it in via a /products/{id} REST call. The webhook endpoint does the final
        // ZeroWeight check after both translation and enrichment have run.
        var body = """
            {
              "id": 99,
              "currency": "EUR",
              "billing": { "first_name": "X", "last_name": "Y", "address_1": "Line", "city": "Amsterdam", "postcode": "1012 AB", "country": "NL" },
              "shipping": { "first_name": "X", "last_name": "Y", "address_1": "Line", "city": "Amsterdam", "postcode": "1012 AB", "country": "NL" },
              "line_items": [ { "id": 1, "name": "T", "sku": "S", "quantity": 1, "price": "10.00", "product_id": 42 } ]
            }
            """;

        var translator = new WooCommerceOrderTranslator();
        var payload = await translator.TranslateAsync(TenantId.New(), "https://shop.example", body, new Dictionary<string, string>(), CancellationToken.None);

        payload.TotalWeight.Grams.Should().Be(0);
        payload.Items.Should().ContainSingle().Which.ExternalProductId.Should().Be("42");
    }

    [Test]
    public async Task Throws_unknown_country_failure_when_destination_country_blank()
    {
        var body = """
            {
              "id": 100,
              "currency": "EUR",
              "billing": { "first_name": "X", "last_name": "Y", "address_1": "Line", "city": "Amsterdam", "postcode": "1012 AB", "country": "NL" },
              "shipping": { "first_name": "X", "last_name": "Y", "address_1": "Line", "city": "Amsterdam", "postcode": "1012 AB", "country": "" },
              "line_items": [ { "id": 1, "name": "T", "sku": "S", "product_id": 1, "quantity": 1, "price": "10.00", "meta_data": [] } ]
            }
            """;

        var translator = new WooCommerceOrderTranslator();
        Func<Task> act = () => translator.TranslateAsync(TenantId.New(), "https://shop.example", body, new Dictionary<string, string>(), CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<IngestionTranslationException>()).Which;
        ex.Code.Should().Be(IngestionReasonCode.UnknownCountry);
        ex.ConnectorCode.Should().Be("woocommerce");
    }
}
