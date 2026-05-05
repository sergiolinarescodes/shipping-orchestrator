using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Domain.ValueObjects;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace ShippingOrchestrator.EcommerceConnectors.WooCommerce.Tests;

/// <summary>
/// Covers <see cref="WooCommerceEcommerceConnector.EnrichAsync"/> against a WireMock-fronted
/// fake WC REST API. These tests are the regression net for the original bug — a WC order
/// webhook lands with zero per-line weight (because the REST order shape doesn't carry it),
/// and the connector must call <c>/wp-json/wc/v3/products/{id}</c> to fill it in. Anything that
/// changes in the line-item → product → weight pipeline must be assertable here.
/// </summary>
[TestFixture]
public class WooCommerceEcommerceConnectorEnrichTests
{
    private WireMockServer _wc = null!;
    private WooCommerceEcommerceConnector _connector = null!;

    [SetUp]
    public void Setup()
    {
        _wc = WireMockServer.Start();
        _connector = new WooCommerceEcommerceConnector(
            new BareClientFactory(),
            Options.Create(new WooCommerceOptions()),
            NullLogger<WooCommerceEcommerceConnector>.Instance);
    }

    [TearDown]
    public void TearDown() => _wc.Stop();

    [Test]
    public async Task Fills_missing_weight_from_product_lookup()
    {
        StubProduct(productId: 42, weight: "1.500");
        var payload = OrderPayload(LineItem("ACME-1", productId: "42", grams: 0));

        var enriched = await _connector.EnrichAsync(payload, CredentialsBundle(), CancellationToken.None);

        enriched.Items[0].UnitWeight.Grams.Should().Be(1500);
        enriched.TotalWeight.Grams.Should().Be(1500);
        _wc.LogEntries.Should().HaveCount(1, "single product fetched once");
    }

    [Test]
    public async Task Skips_fetch_when_every_line_already_has_weight()
    {
        // Translator-supplied weight (legacy plugin or simulator) must short-circuit the
        // REST hop. Otherwise we'd burn round trips on every webhook for no gain.
        var payload = OrderPayload(LineItem("S", productId: "42", grams: 800));

        var enriched = await _connector.EnrichAsync(payload, CredentialsBundle(), CancellationToken.None);

        enriched.Items[0].UnitWeight.Grams.Should().Be(800);
        _wc.LogEntries.Should().BeEmpty("no product lookup should happen if everything has weight");
    }

    [Test]
    public async Task Deduplicates_product_fetches_when_multiple_lines_reference_same_product()
    {
        // Two cart lines for the same product (e.g. customer added the SKU twice) must result
        // in a single REST hop, not one per line.
        StubProduct(productId: 42, weight: "0.250");
        var payload = OrderPayload(
            LineItem("ACME-1", productId: "42", grams: 0, qty: 1),
            LineItem("ACME-1", productId: "42", grams: 0, qty: 2));

        var enriched = await _connector.EnrichAsync(payload, CredentialsBundle(), CancellationToken.None);

        enriched.Items[0].UnitWeight.Grams.Should().Be(250);
        enriched.Items[1].UnitWeight.Grams.Should().Be(250);
        enriched.TotalWeight.Grams.Should().Be(750, "1×250 + 2×250 = 750");
        _wc.LogEntries.Should().HaveCount(1, "duplicate product ids must collapse to one fetch");
    }

    [Test]
    public async Task Leaves_zero_weight_when_product_is_404()
    {
        // WC product was deleted between order placement and enrichment. We can't recover the
        // weight; the line stays zero. The webhook endpoint's post-enrich validation then
        // surfaces the ZeroWeight failure with the tenant-readable hint.
        _wc.Given(Request.Create().WithPath("/wp-json/wc/v3/products/42").UsingGet())
           .RespondWith(Response.Create().WithStatusCode(404));

        var payload = OrderPayload(LineItem("S", productId: "42", grams: 0));

        var enriched = await _connector.EnrichAsync(payload, CredentialsBundle(), CancellationToken.None);

        enriched.Items[0].UnitWeight.Grams.Should().Be(0);
    }

    [Test]
    public async Task Leaves_zero_weight_when_product_has_blank_or_zero_weight()
    {
        // Tenant configured the product but never typed a weight — WC returns weight: "" or
        // weight: "0". Both must be treated as missing, not as 0g.
        _wc.Given(Request.Create().WithPath("/wp-json/wc/v3/products/42").UsingGet())
           .RespondWith(Response.Create().WithBodyAsJson(new { id = 42, weight = "" }));
        _wc.Given(Request.Create().WithPath("/wp-json/wc/v3/products/43").UsingGet())
           .RespondWith(Response.Create().WithBodyAsJson(new { id = 43, weight = "0" }));

        var payload = OrderPayload(
            LineItem("BLANK", productId: "42", grams: 0),
            LineItem("ZERO", productId: "43", grams: 0));

        var enriched = await _connector.EnrichAsync(payload, CredentialsBundle(), CancellationToken.None);

        enriched.Items[0].UnitWeight.Grams.Should().Be(0);
        enriched.Items[1].UnitWeight.Grams.Should().Be(0);
        enriched.TotalWeight.Grams.Should().Be(0,
            "downstream validation surfaces ZeroWeight; the enricher must not invent grams");
    }

    [Test]
    public async Task Returns_payload_unchanged_when_credentials_bundle_is_empty()
    {
        // Defensive: a connector reachable without credentials must no-op rather than crash.
        // Recheck flow could theoretically hit this if the credential cipher decrypt failed.
        var payload = OrderPayload(LineItem("S", productId: "42", grams: 0));

        var enriched = await _connector.EnrichAsync(payload, decryptedCredentials: [], CancellationToken.None);

        enriched.Should().BeSameAs(payload, "no credentials → must be a no-op pass-through");
        _wc.LogEntries.Should().BeEmpty();
    }

    [Test]
    public async Task Returns_payload_unchanged_when_no_line_items_need_enrichment()
    {
        // Already-weighted payload where every line has weight AND every line is missing
        // ExternalProductId (e.g. simulator builds). Must short-circuit before deserializing
        // credentials so a bogus bundle doesn't take down a perfectly valid order.
        var payload = OrderPayload(LineItem("S", productId: null, grams: 500));

        var enriched = await _connector.EnrichAsync(payload, decryptedCredentials: "not-json"u8.ToArray(), CancellationToken.None);

        enriched.Should().BeSameAs(payload);
    }

    [Test]
    public async Task Mixes_translator_supplied_weight_with_enricher_filled_weight()
    {
        // One line had _weight meta from a plugin (translator filled it); the other line is
        // bare and needs an enricher fetch. The enricher must touch only the bare line.
        StubProduct(productId: 99, weight: "0.300");
        var payload = OrderPayload(
            LineItem("HAS_WEIGHT", productId: "1", grams: 700),
            LineItem("NEEDS_FETCH", productId: "99", grams: 0));

        var enriched = await _connector.EnrichAsync(payload, CredentialsBundle(), CancellationToken.None);

        enriched.Items[0].UnitWeight.Grams.Should().Be(700, "translator-supplied weight is preserved");
        enriched.Items[1].UnitWeight.Grams.Should().Be(300);
        _wc.LogEntries.Should().OnlyContain(
            e => e.RequestMessage!.Path == "/wp-json/wc/v3/products/99",
            "only the unweighted line should trigger a REST hop");
    }

    [Test]
    public async Task Converts_product_dimensions_from_cm_to_mm()
    {
        // WC stores dimensions in cm by default; the orchestrator's Dimension is in mm.
        // A 10×20×30 cm product must land as 100×200×300 mm. Regression target: any unit
        // conversion bug here will cap the wrong shipping rate downstream.
        _wc.Given(Request.Create().WithPath("/wp-json/wc/v3/products/42").UsingGet())
           .RespondWith(Response.Create().WithBodyAsJson(new
           {
               id = 42,
               weight = "1.0",
               dimensions = new { length = "10", width = "20", height = "30" },
           }));

        var payload = OrderPayload(LineItem("S", productId: "42", grams: 0));

        var enriched = await _connector.EnrichAsync(payload, CredentialsBundle(), CancellationToken.None);

        enriched.PackageDimensions.LengthMm.Should().Be(100);
        enriched.PackageDimensions.WidthMm.Should().Be(200);
        enriched.PackageDimensions.HeightMm.Should().Be(300);
    }

    private void StubProduct(long productId, string weight) =>
        _wc.Given(Request.Create().WithPath($"/wp-json/wc/v3/products/{productId}").UsingGet())
           .RespondWith(Response.Create().WithBodyAsJson(new { id = productId, weight }));

    private byte[] CredentialsBundle() =>
        JsonSerializer.SerializeToUtf8Bytes(new WooCommerceCredentialsBundle(
            ConsumerKey: "ck_test",
            ConsumerSecret: "cs_test",
            StoreUrl: _wc.Urls[0],
            WebhookSecret: "secret"));

    private static EcommerceOrderLineItem LineItem(string sku, string? productId, int grams, int qty = 1) =>
        new(Sku: sku,
            Title: sku,
            Quantity: qty,
            UnitPrice: new Money(10m, "EUR"),
            UnitWeight: Weight.FromGrams(grams),
            ExternalProductId: productId);

    private static EcommerceOrderPayload OrderPayload(params EcommerceOrderLineItem[] items)
    {
        var nl = new CountryCode("NL");
        var origin = new Address("Origin", "Line", null, "Hoofddorp", null, "2132 AA", nl);
        var dest = new Address("Customer", "Line", null, "Amsterdam", null, "1012 LM", nl);
        return new EcommerceOrderPayload(
            TenantId: TenantId.New(),
            ConnectorCode: "woocommerce",
            ExternalOrderId: "1",
            Currency: "EUR",
            From: origin,
            To: dest,
            Items: items,
            TotalWeight: Weight.FromGrams(items.Sum(i => i.UnitWeight.Grams * i.Quantity)),
            PackageDimensions: new Dimension(LengthMm: 250, WidthMm: 200, HeightMm: 100));
    }

    /// <summary>
    /// Plain factory — connector overwrites <c>BaseAddress</c> from the credentials bundle's
    /// <c>StoreUrl</c>, which the test wires to point at WireMock.
    /// </summary>
    private sealed class BareClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
