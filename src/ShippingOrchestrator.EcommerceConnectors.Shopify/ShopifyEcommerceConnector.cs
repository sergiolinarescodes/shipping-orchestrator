using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;

namespace ShippingOrchestrator.EcommerceConnectors.Shopify;

public sealed class ShopifyEcommerceConnector(
    IHttpClientFactory httpFactory,
    IOptions<ShopifyOptions> options,
    ILogger<ShopifyEcommerceConnector> log) : IEcommerceConnector, IEcommerceOrderFetcher
{
    public const string HttpClientName = "shopify";
    private readonly ShopifyOptions _options = options.Value;

    public string PlatformCode => "shopify";

    public Task<OAuthInstallUrl> BuildInstallUrlAsync(OAuthInstallRequest request, CancellationToken ct)
    {
        var authority = _options.AuthorityOverride
            ?? $"https://{request.ExternalAccountId}";
        var url = $"{authority.TrimEnd('/')}/admin/oauth/authorize" +
                  $"?client_id={Uri.EscapeDataString(_options.ClientId)}" +
                  $"&scope={Uri.EscapeDataString(_options.Scopes)}" +
                  $"&redirect_uri={Uri.EscapeDataString(request.RedirectUri)}" +
                  $"&state={Uri.EscapeDataString(request.State)}";
        return Task.FromResult(new OAuthInstallUrl(url));
    }

    public async Task<OAuthInstallResult> CompleteOAuthAsync(OAuthCallback callback, CancellationToken ct)
    {
        var authority = _options.AuthorityOverride
            ?? $"https://{callback.ExternalAccountId}";
        var endpoint = $"{authority.TrimEnd('/')}/admin/oauth/access_token";

        using var client = httpFactory.CreateClient(HttpClientName);
        var response = await client.PostAsJsonAsync(endpoint, new
        {
            client_id = _options.ClientId,
            client_secret = _options.ClientSecret,
            code = callback.Code,
        }, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            log.LogWarning("Shopify token exchange failed: {Status} {Body}", response.StatusCode, body);
            return new OAuthInstallResult(false, null, null, $"shopify token endpoint returned {(int)response.StatusCode}");
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct).ConfigureAwait(false);
        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            return new OAuthInstallResult(false, null, null, "shopify token response missing access_token");

        // Auto-register webhooks via Admin API so orders/fulfillments dispatch to us. Mirrors
        // the WooCommerce pattern (which uses WC REST). Skipped silently when no public
        // webhook base URL is configured — useful for unit tests and CI where there's no
        // outward-facing host.
        if (!string.IsNullOrWhiteSpace(_options.OrchestratorWebhookBaseUrl))
        {
            try
            {
                await RegisterWebhooksAsync(callback.ExternalAccountId, token.AccessToken, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                log.LogWarning(ex, "Shopify webhook auto-registration failed for {Shop}; install completes but orders won't flow until webhooks are registered.", callback.ExternalAccountId);
            }
        }
        else
        {
            log.LogWarning("Connectors:Shopify:OrchestratorWebhookBaseUrl not configured — Shopify will not deliver webhooks for shop {Shop}. Set it to a publicly reachable URL (cloudflared / ngrok in dev, your API host in prod) and reconnect the store.", callback.ExternalAccountId);
        }

        var payload = System.Text.Encoding.UTF8.GetBytes(token.AccessToken);
        return new OAuthInstallResult(true, callback.ExternalAccountId, payload);
    }

    private async Task RegisterWebhooksAsync(string shop, string accessToken, CancellationToken ct)
    {
        var deliveryUrl = $"{_options.OrchestratorWebhookBaseUrl!.TrimEnd('/')}/v1/webhooks/shopify";
        var authority = _options.AuthorityOverride ?? $"https://{shop}";
        using var client = httpFactory.CreateClient(HttpClientName);
        client.DefaultRequestHeaders.Remove("X-Shopify-Access-Token");
        client.DefaultRequestHeaders.Add("X-Shopify-Access-Token", accessToken);

        await PurgeOrchestratorWebhooksAsync(client, authority, deliveryUrl, ct).ConfigureAwait(false);

        var topics = new[] { "orders/create", "orders/updated", "orders/paid", "fulfillments/create", "app/uninstalled" };
        foreach (var topic in topics)
        {
            var body = new { webhook = new { topic, address = deliveryUrl, format = "json" } };
            using var resp = await client
                .PostAsJsonAsync($"{authority.TrimEnd('/')}/admin/api/{_options.ApiVersion}/webhooks.json", body, ct)
                .ConfigureAwait(false);
            // 422 with "for this topic and address" means already registered — fine.
            if (!resp.IsSuccessStatusCode && resp.StatusCode != System.Net.HttpStatusCode.UnprocessableEntity)
            {
                var err = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                throw new HttpRequestException($"Shopify webhook registration for topic '{topic}' returned {(int)resp.StatusCode}: {err}");
            }
        }
        log.LogInformation("Registered Shopify webhooks for {Shop} → {DeliveryUrl}", shop, deliveryUrl);
    }

    private async Task PurgeOrchestratorWebhooksAsync(HttpClient client, string authority, string deliveryUrl, CancellationToken ct)
    {
        var listUrl = $"{authority.TrimEnd('/')}/admin/api/{_options.ApiVersion}/webhooks.json?address={Uri.EscapeDataString(deliveryUrl)}";
        using var listResp = await client.GetAsync(listUrl, ct).ConfigureAwait(false);
        if (!listResp.IsSuccessStatusCode)
        {
            log.LogWarning("Could not list existing Shopify webhooks for cleanup ({Status}); skipping purge.", (int)listResp.StatusCode);
            return;
        }
        var existing = await listResp.Content.ReadFromJsonAsync<ShopifyWebhookListResponse>(cancellationToken: ct).ConfigureAwait(false);
        if (existing?.Webhooks is null) return;
        foreach (var hook in existing.Webhooks)
        {
            using var del = await client
                .DeleteAsync($"{authority.TrimEnd('/')}/admin/api/{_options.ApiVersion}/webhooks/{hook.Id}.json", ct)
                .ConfigureAwait(false);
            if (del.IsSuccessStatusCode)
                log.LogInformation("Removed stale Shopify webhook id={Id} topic={Topic}", hook.Id, hook.Topic);
        }
    }

    private sealed record ShopifyWebhookListResponse(
        [property: JsonPropertyName("webhooks")] IReadOnlyList<ShopifyWebhookSummary>? Webhooks);

    private sealed record ShopifyWebhookSummary(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("topic")] string Topic,
        [property: JsonPropertyName("address")] string Address);

    public Task<WebhookHandled> HandleWebhookAsync(RawWebhook webhook, CancellationToken ct)
    {
        // First-cut: accept anything signed by Shopify; HMAC validation lands with the real
        // webhook subscription. Test scenarios drive this path with stubbed payloads.
        return Task.FromResult(new WebhookHandled(true));
    }

    /// <summary>
    /// Pulls a single order from the Shopify Admin REST API for the Recheck flow.
    /// Returns the order body as JSON in the same shape <see cref="ShopifyOrderTranslator"/>
    /// expects from a webhook — i.e. unwrapped (the translator deserializes the order
    /// directly, not the <c>{"order":{...}}</c> envelope the REST API returns).
    /// </summary>
    public async Task<OrderFetchResult> FetchRawOrderAsync(OrderFetchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.DecryptedCredentials.Length == 0)
            return new OrderFetchResult(false, null, "no credentials available for this connection");

        var accessToken = System.Text.Encoding.UTF8.GetString(request.DecryptedCredentials);
        var authority = _options.AuthorityOverride ?? $"https://{request.ExternalAccountId}";
        var url = $"{authority.TrimEnd('/')}/admin/api/{_options.ApiVersion}/orders/{Uri.EscapeDataString(request.ExternalOrderId)}.json";

        using var client = httpFactory.CreateClient(HttpClientName);
        client.DefaultRequestHeaders.Remove("X-Shopify-Access-Token");
        client.DefaultRequestHeaders.Add("X-Shopify-Access-Token", accessToken);

        try
        {
            using var resp = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return new OrderFetchResult(false, null, $"Shopify returned 404 for order {request.ExternalOrderId}.");
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                log.LogWarning("Shopify order fetch {OrderId} on shop {Shop} returned {Status}: {Body}",
                    request.ExternalOrderId, request.ExternalAccountId, (int)resp.StatusCode, err);
                return new OrderFetchResult(false, null, $"Shopify returned {(int)resp.StatusCode}.");
            }

            var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            // Admin API returns {"order": {...}}. The webhook delivers the inner object directly,
            // and the translator was written for that. Unwrap so the translator runs unchanged.
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("order", out var orderElement))
                return new OrderFetchResult(false, null, "Shopify response did not contain an 'order' object.");
            return new OrderFetchResult(true, orderElement.GetRawText(), null);
        }
        catch (HttpRequestException ex)
        {
            log.LogWarning(ex, "Shopify order fetch {OrderId} on shop {Shop} failed.",
                request.ExternalOrderId, request.ExternalAccountId);
            return new OrderFetchResult(false, null, $"Could not reach Shopify: {ex.Message}");
        }
    }

    public Task PushFulfillmentAsync(TenantId tenantId, FulfillmentUpdate update, CancellationToken ct)
    {
        log.LogInformation("Shopify push fulfillment for tenant {Tenant}, order {OrderId}, tracking {Tracking}",
            tenantId, update.ExternalOrderId, update.TrackingNumber);
        return Task.CompletedTask;
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("scope")] string Scope);
}
