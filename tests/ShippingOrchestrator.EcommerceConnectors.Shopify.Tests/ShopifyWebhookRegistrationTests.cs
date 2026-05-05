using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;

namespace ShippingOrchestrator.EcommerceConnectors.Shopify.Tests;

/// <summary>
/// Without these assertions, a regression where <c>CompleteOAuthAsync</c> exchanges the OAuth
/// code but forgets to register webhooks would slip through every E2E test (which post webhook
/// bodies directly at the orchestrator endpoint and never exercise the registration call).
/// That regression bit us in production: stores were "Active" in the dashboard but Shopify
/// never delivered any orders because no webhook was registered against the public host.
/// These tests verify the connector hits Shopify Admin's webhooks.json endpoint with the
/// expected delivery URL + topic set whenever <c>OrchestratorWebhookBaseUrl</c> is configured.
/// </summary>
[TestFixture]
public class ShopifyWebhookRegistrationTests
{
    private const string Shop = "acme-test.myshopify.com";
    private const string AccessToken = "shpat_test_token";
    private const string PublicBase = "https://api.ship-shipping.test";
    private static readonly string ExpectedDelivery = $"{PublicBase}/v1/webhooks/shopify";

    [Test]
    public async Task CompleteOAuthAsync_registers_webhooks_for_every_topic_against_configured_public_base_url()
    {
        var captured = new List<HttpRequestMessage>();
        var handler = new ScriptedHandler(req =>
        {
            captured.Add(req);
            // Token exchange.
            if (req.RequestUri!.AbsolutePath.EndsWith("/admin/oauth/access_token", StringComparison.Ordinal))
                return JsonResponse(HttpStatusCode.OK, new { access_token = AccessToken, scope = "read_orders,write_fulfillments" });
            // Existing-webhook list (purge step).
            if (req.Method == HttpMethod.Get && req.RequestUri.AbsolutePath.EndsWith("/webhooks.json", StringComparison.Ordinal))
                return JsonResponse(HttpStatusCode.OK, new { webhooks = Array.Empty<object>() });
            // Webhook create.
            if (req.Method == HttpMethod.Post && req.RequestUri.AbsolutePath.EndsWith("/webhooks.json", StringComparison.Ordinal))
                return JsonResponse(HttpStatusCode.Created, new { webhook = new { id = 1L } });
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var connector = BuildConnector(handler, opts =>
        {
            opts.ClientId = "client-id";
            opts.ClientSecret = "client-secret";
            opts.OrchestratorWebhookBaseUrl = PublicBase;
        });

        var result = await connector.CompleteOAuthAsync(
            new OAuthCallback(TenantId.New(), Shop, "auth-code", "state-token", new Dictionary<string, string>()),
            CancellationToken.None);

        result.Success.Should().BeTrue(result.FailureReason);

        var registrations = captured
            .Where(r => r.Method == HttpMethod.Post && r.RequestUri!.AbsolutePath.EndsWith("/webhooks.json", StringComparison.Ordinal))
            .ToArray();
        registrations.Should().HaveCount(5, "one POST per registered topic");

        var topicsAndAddresses = await Task.WhenAll(registrations.Select(async r =>
        {
            var doc = JsonDocument.Parse(await r.Content!.ReadAsStringAsync());
            var w = doc.RootElement.GetProperty("webhook");
            return (Topic: w.GetProperty("topic").GetString(), Address: w.GetProperty("address").GetString());
        }));

        topicsAndAddresses.Select(t => t.Topic).Should()
            .BeEquivalentTo(["orders/create", "orders/updated", "orders/paid", "fulfillments/create", "app/uninstalled"]);
        topicsAndAddresses.Select(t => t.Address).Should().AllBe(ExpectedDelivery,
            "delivery URL must match the configured OrchestratorWebhookBaseUrl");

        registrations.All(r =>
            r.Headers.GetValues("X-Shopify-Access-Token").Single() == AccessToken)
            .Should().BeTrue("registration calls must carry the access token granted by OAuth");
    }

    [Test]
    public async Task CompleteOAuthAsync_skips_registration_when_OrchestratorWebhookBaseUrl_is_not_configured()
    {
        var captured = new List<HttpRequestMessage>();
        var handler = new ScriptedHandler(req =>
        {
            captured.Add(req);
            return JsonResponse(HttpStatusCode.OK, new { access_token = AccessToken, scope = "read_orders" });
        });

        var connector = BuildConnector(handler, opts =>
        {
            opts.ClientId = "client-id";
            opts.ClientSecret = "client-secret";
            opts.OrchestratorWebhookBaseUrl = null;
        });

        var result = await connector.CompleteOAuthAsync(
            new OAuthCallback(TenantId.New(), Shop, "auth-code", "state-token", new Dictionary<string, string>()),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        captured.Should().HaveCount(1, "only the token-exchange call should fire when no public base URL is configured");
        captured[0].RequestUri!.AbsolutePath.Should().EndWith("/admin/oauth/access_token");
    }

    [Test]
    public async Task CompleteOAuthAsync_completes_install_even_if_webhook_registration_fails()
    {
        var handler = new ScriptedHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/admin/oauth/access_token", StringComparison.Ordinal))
                return JsonResponse(HttpStatusCode.OK, new { access_token = AccessToken, scope = "read_orders" });
            if (req.Method == HttpMethod.Get && req.RequestUri.AbsolutePath.EndsWith("/webhooks.json", StringComparison.Ordinal))
                return JsonResponse(HttpStatusCode.OK, new { webhooks = Array.Empty<object>() });
            // Simulate Shopify rejecting the registration (e.g. address not HTTPS in prod).
            return JsonResponse(HttpStatusCode.UnprocessableEntity, new { errors = new { address = new[] { "is invalid" } } });
        });

        var connector = BuildConnector(handler, opts =>
        {
            opts.ClientId = "client-id";
            opts.ClientSecret = "client-secret";
            opts.OrchestratorWebhookBaseUrl = PublicBase;
        });

        var result = await connector.CompleteOAuthAsync(
            new OAuthCallback(TenantId.New(), Shop, "auth-code", "state-token", new Dictionary<string, string>()),
            CancellationToken.None);

        // Registration failure is logged but doesn't block install — the operator can re-run
        // registration later via reconnect / manual retry. Treating it as fatal would leave
        // tenants stranded if Shopify temporarily rate-limits the webhook API.
        result.Success.Should().BeTrue("install completes; webhook failure is recoverable via reconnect");
    }

    private static ShopifyEcommerceConnector BuildConnector(ScriptedHandler handler, Action<ShopifyOptions> configure)
    {
        var opts = new ShopifyOptions
        {
            AuthorityOverride = "https://wiremock.test", // route all Shopify-bound traffic through the handler
        };
        configure(opts);
        var factory = new SingleHandlerHttpClientFactory(handler);
        return new ShopifyEcommerceConnector(factory, Options.Create(opts), NullLogger<ShopifyEcommerceConnector>.Instance);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object body) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
    };

    private sealed class ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class SingleHandlerHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
