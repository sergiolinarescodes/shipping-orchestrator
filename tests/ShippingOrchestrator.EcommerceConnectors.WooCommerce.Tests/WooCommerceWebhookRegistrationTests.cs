using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;

namespace ShippingOrchestrator.EcommerceConnectors.WooCommerce.Tests;

/// <summary>
/// Companion to the Shopify webhook-registration tests. Same regression risk: a connector
/// that successfully completes OAuth but quietly fails to register webhooks leaves the store
/// looking healthy in the dashboard while no orders ever flow. These assertions wire-up the
/// WP REST endpoint via a scripted message handler and verify the connector lists, purges,
/// and re-registers webhooks at install time, then surfaces a sane failure when WP rejects.
/// </summary>
[TestFixture]
public class WooCommerceWebhookRegistrationTests
{
    private const string Store = "https://shop.example.test";
    private const string ConsumerKey = "ck_e2e";
    private const string ConsumerSecret = "cs_e2e";
    private const string DeliveryUrl = "http://orchestrator:5101/v1/webhooks/woocommerce";

    [Test]
    public async Task CompleteOAuthAsync_purges_stale_webhooks_then_registers_topics_with_matching_secret()
    {
        var captured = new List<(HttpMethod Method, string Path, string Body)>();
        int nextId = 100;
        var handler = new ScriptedHandler(async req =>
        {
            var body = req.Content is null ? string.Empty : await req.Content.ReadAsStringAsync();
            captured.Add((req.Method, req.RequestUri!.AbsolutePath, body));
            if (req.Method == HttpMethod.Get && req.RequestUri.AbsolutePath.EndsWith("/wp-json/wc/v3/webhooks", StringComparison.Ordinal))
                return JsonResponse(HttpStatusCode.OK, new[] {
                    new { id = 7, name = "Ship Shipping (order.created)", delivery_url = "http://old/path" },
                    new { id = 99, name = "Some Other Plugin Hook", delivery_url = "http://other/path" },
                });
            if (req.Method == HttpMethod.Delete && req.RequestUri.AbsolutePath.Contains("/wp-json/wc/v3/webhooks/", StringComparison.Ordinal))
                return JsonResponse(HttpStatusCode.OK, new { id = 7 });
            if (req.Method == HttpMethod.Post && req.RequestUri.AbsolutePath.EndsWith("/wp-json/wc/v3/webhooks", StringComparison.Ordinal))
                return JsonResponse(HttpStatusCode.Created, new { id = nextId++ });
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var connector = BuildConnector(handler, opts =>
        {
            opts.OrchestratorWebhookUrl = DeliveryUrl;
        });

        var result = await connector.CompleteOAuthAsync(
            new OAuthCallback(TenantId.New(), Store, "code", "state", new Dictionary<string, string>
            {
                ["consumer_key"] = ConsumerKey,
                ["consumer_secret"] = ConsumerSecret,
            }),
            CancellationToken.None);

        result.Success.Should().BeTrue(result.FailureReason);
        result.CredentialsPayload.Should().NotBeNull();

        captured.Should().Contain(c =>
            c.Method == HttpMethod.Get && c.Path == "/wp-json/wc/v3/webhooks",
            "must list existing webhooks before registering");
        captured.Should().Contain(c =>
            c.Method == HttpMethod.Delete && c.Path == "/wp-json/wc/v3/webhooks/7",
            "stale Ship-Shipping-prefixed webhooks must be deleted on re-install");
        captured.Should().NotContain(c =>
            c.Method == HttpMethod.Delete && c.Path == "/wp-json/wc/v3/webhooks/99",
            "third-party webhooks must NOT be touched");

        var registrations = captured.Where(c => c.Method == HttpMethod.Post && c.Path.EndsWith("/wp-json/wc/v3/webhooks", StringComparison.Ordinal)).ToArray();
        registrations.Should().HaveCount(2);

        WebhookCreate? createdSecret = null;
        foreach (var (_, _, body) in registrations)
        {
            var parsed = JsonSerializer.Deserialize<WebhookCreate>(body);
            parsed.Should().NotBeNull();
            parsed!.delivery_url.Should().Be(DeliveryUrl);
            parsed.status.Should().Be("active");
            parsed.secret.Should().NotBeNullOrEmpty();
            createdSecret ??= parsed;
            parsed.secret.Should().Be(createdSecret.secret, "the same secret must be reused for every topic in one install");
        }

        // Persisted credentials bundle must carry the SAME secret we sent to WC, otherwise
        // inbound webhook HMAC validation will reject every delivery (the regression that
        // shipped to production: orchestrator stored secret_A, WC signed with secret_B,
        // every delivery 401'd).
        var bundle = JsonSerializer.Deserialize<WooCommerceCredentialsBundle>(result.CredentialsPayload!);
        bundle.Should().NotBeNull();
        bundle!.WebhookSecret.Should().Be(createdSecret!.secret,
            "stored credentials bundle's WebhookSecret must equal the secret POSTed to WC");
        bundle.ConsumerKey.Should().Be(ConsumerKey);
        bundle.ConsumerSecret.Should().Be(ConsumerSecret);
    }

    [Test]
    public async Task CompleteOAuthAsync_returns_failure_when_webhook_registration_fails()
    {
        var handler = new ScriptedHandler(req =>
        {
            if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath.EndsWith("/wp-json/wc/v3/webhooks", StringComparison.Ordinal))
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, Array.Empty<object>()));
            return Task.FromResult(JsonResponse(HttpStatusCode.BadRequest,
                new { code = "rest_invalid", message = "delivery_url must be reachable" }));
        });

        var connector = BuildConnector(handler, opts => opts.OrchestratorWebhookUrl = DeliveryUrl);

        var result = await connector.CompleteOAuthAsync(
            new OAuthCallback(TenantId.New(), Store, "code", "state", new Dictionary<string, string>
            {
                ["consumer_key"] = ConsumerKey,
                ["consumer_secret"] = ConsumerSecret,
            }),
            CancellationToken.None);

        // WC behaviour mirror: registration is mandatory (the connector exists to wire WC
        // events into our pipeline). Surfacing failure instead of silently persisting an
        // un-wired connection is the only way the dashboard can tell the operator the
        // install didn't fully take.
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("webhook registration failed");
    }

    private static WooCommerceEcommerceConnector BuildConnector(ScriptedHandler handler, Action<WooCommerceOptions> configure)
    {
        var opts = new WooCommerceOptions { AuthorityOverride = Store };
        configure(opts);
        var factory = new SingleHandlerHttpClientFactory(handler);
        return new WooCommerceEcommerceConnector(factory, Options.Create(opts), NullLogger<WooCommerceEcommerceConnector>.Instance);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object body) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
    };

    private sealed class ScriptedHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            respond(request);
    }

    private sealed class SingleHandlerHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed record WebhookCreate(string name, string topic, string delivery_url, string secret, string status);
}
