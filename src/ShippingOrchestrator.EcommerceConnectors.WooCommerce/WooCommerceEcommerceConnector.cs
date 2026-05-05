using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Domain.ValueObjects;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;

namespace ShippingOrchestrator.EcommerceConnectors.WooCommerce;

/// <summary>
/// Real WooCommerce connector. Uses WC's built-in Authentication Endpoint
/// (<c>/wc-auth/v1/authorize</c>) to acquire a consumer key/secret pair from a logged-in WP
/// admin, then auto-registers webhooks via the WC REST API. The <c>OAuthInstallResult.CredentialsPayload</c>
/// is a JSON bundle of <c>{consumer_key, consumer_secret, store_url, webhook_secret}</c> so the
/// downstream pipeline can both call WC REST and verify inbound webhook HMACs without re-decoding.
/// </summary>
public sealed class WooCommerceEcommerceConnector(
    IHttpClientFactory httpFactory,
    IOptions<WooCommerceOptions> options,
    ILogger<WooCommerceEcommerceConnector> log) : IEcommerceConnector, IEcommerceOrderFetcher, IEcommerceOrderEnricher
{
    public const string HttpClientName = "woocommerce";
    private readonly WooCommerceOptions _options = options.Value;

    public string PlatformCode => "woocommerce";

    public Task<OAuthInstallUrl> BuildInstallUrlAsync(OAuthInstallRequest request, CancellationToken ct)
    {
        var storeAuthority = NormalizeStoreUrl(_options.AuthorityOverride ?? request.ExternalAccountId);
        var callbackUrl = $"{_options.CallbackBaseUrl.TrimEnd('/')}/v1/connections/dashboard-callback/woocommerce";
        var url = $"{storeAuthority}/wc-auth/v1/authorize" +
                  $"?app_name={Uri.EscapeDataString(_options.AppName)}" +
                  $"&scope={Uri.EscapeDataString(_options.Scope)}" +
                  $"&user_id={Uri.EscapeDataString(request.State)}" +
                  $"&return_url={Uri.EscapeDataString(_options.ReturnUrl)}" +
                  $"&callback_url={Uri.EscapeDataString(callbackUrl)}";
        return Task.FromResult(new OAuthInstallUrl(url));
    }

    /// <summary>
    /// Completes the WC key-exchange. The OAuth-callback endpoint received the JSON body WP
    /// POSTed and parsed <c>consumer_key</c>/<c>consumer_secret</c> out of it. We then call
    /// WC REST to register webhooks pointing at the orchestrator and return the encrypted
    /// credential bundle (keys + webhook secret) for persistence.
    /// </summary>
    public async Task<OAuthInstallResult> CompleteOAuthAsync(OAuthCallback callback, CancellationToken ct)
    {
        if (!callback.AdditionalParameters.TryGetValue("consumer_key", out var consumerKey)
            || !callback.AdditionalParameters.TryGetValue("consumer_secret", out var consumerSecret))
        {
            return new OAuthInstallResult(false, null, null, "WooCommerce callback missing consumer_key/consumer_secret.");
        }

        var storeUrl = NormalizeStoreUrl(_options.AuthorityOverride ?? callback.ExternalAccountId);
        var webhookSecret = GenerateWebhookSecret();

        try
        {
            await RegisterWebhooksAsync(storeUrl, consumerKey, consumerSecret, webhookSecret, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            log.LogWarning(ex, "WooCommerce webhook auto-registration failed for {Store}", storeUrl);
            return new OAuthInstallResult(false, null, null, $"webhook registration failed: {ex.Message}");
        }

        var bundle = JsonSerializer.SerializeToUtf8Bytes(new WooCommerceCredentialsBundle(
            ConsumerKey: consumerKey,
            ConsumerSecret: consumerSecret,
            StoreUrl: storeUrl,
            WebhookSecret: webhookSecret));
        return new OAuthInstallResult(true, storeUrl, bundle);
    }

    /// <summary>
    /// Canonicalizes a WC store URL so the same merchant-typed value always hashes the same in
    /// the connections table, regardless of trailing slash or case. WC's outbound webhook
    /// header <c>X-WC-Webhook-Source</c> is the value of <c>home_url()</c> (lowercase host,
    /// no trailing slash); aligning the stored form with it avoids spurious 404s on
    /// <c>FindByPlatformAccountAsync</c> at webhook time. Public so the inbound webhook handler
    /// can normalize its lookup key the same way.
    /// </summary>
    public static string NormalizeStoreUrl(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        var trimmed = raw.Trim().TrimEnd('/');
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            var builder = new UriBuilder(uri)
            {
                Host = uri.Host.ToLowerInvariant(),
                Scheme = uri.Scheme.ToLowerInvariant(),
            };
            // UriBuilder appends a default port and trailing slash on path — strip both back out.
            var rebuilt = builder.Uri.GetLeftPart(UriPartial.Authority) + uri.AbsolutePath.TrimEnd('/');
            return rebuilt.TrimEnd('/');
        }
        return trimmed;
    }

    /// <summary>
    /// Fills in per-line product weights that the order webhook payload doesn't carry inline.
    /// WC's REST <c>orders/{id}</c> shape lists line items with <c>product_id</c> + <c>quantity</c>
    /// only — weight (and dimensions) live on <c>products/{id}</c>. We hit each unique product
    /// once and apply the weight to every line that referenced it. Dimensions are taken from
    /// the first line item that supplies usable values.
    /// </summary>
    public async Task<EcommerceOrderPayload> EnrichAsync(
        EcommerceOrderPayload payload,
        byte[] decryptedCredentials,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (decryptedCredentials is null || decryptedCredentials.Length == 0) return payload;

        var missingWeight = payload.Items.Any(i => i.UnitWeight.Grams <= 0 && !string.IsNullOrEmpty(i.ExternalProductId));
        if (!missingWeight) return payload;

        WooCommerceCredentialsBundle? bundle;
        try { bundle = JsonSerializer.Deserialize<WooCommerceCredentialsBundle>(decryptedCredentials); }
        catch (JsonException ex)
        {
            log.LogWarning(ex, "WooCommerce credentials bundle could not be parsed during enrichment.");
            return payload;
        }
        if (bundle is null) return payload;

        using var client = httpFactory.CreateClient(HttpClientName);
        client.BaseAddress = new Uri(bundle.StoreUrl.TrimEnd('/') + "/");
        var qs = $"?consumer_key={Uri.EscapeDataString(bundle.ConsumerKey)}&consumer_secret={Uri.EscapeDataString(bundle.ConsumerSecret)}";

        var distinctIds = payload.Items
            .Where(i => i.UnitWeight.Grams <= 0 && !string.IsNullOrEmpty(i.ExternalProductId))
            .Select(i => i.ExternalProductId!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var weightByProduct = new Dictionary<string, int>(StringComparer.Ordinal);
        WcProductDto? firstWithDimensions = null;

        foreach (var productId in distinctIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var product = await FetchProductAsync(client, qs, productId, cancellationToken).ConfigureAwait(false);
            if (product is null) continue;
            var grams = ParseProductWeightGrams(product.Weight);
            if (grams > 0) weightByProduct[productId] = grams;
            if (firstWithDimensions is null && HasUsableDimensions(product.Dimensions)) firstWithDimensions = product;
        }

        if (weightByProduct.Count == 0 && firstWithDimensions is null) return payload;

        var enrichedItems = payload.Items
            .Select(i =>
            {
                if (i.UnitWeight.Grams > 0
                    || string.IsNullOrEmpty(i.ExternalProductId)
                    || !weightByProduct.TryGetValue(i.ExternalProductId, out var grams))
                {
                    return i;
                }
                return i with { UnitWeight = Weight.FromGrams(grams) };
            })
            .ToArray();

        var totalGrams = enrichedItems.Sum(i => i.UnitWeight.Grams * i.Quantity);
        var dimensions = firstWithDimensions is not null
            ? ToDimension(firstWithDimensions.Dimensions!)
            : payload.PackageDimensions;

        return payload with
        {
            Items = enrichedItems,
            TotalWeight = Weight.FromGrams(totalGrams),
            PackageDimensions = dimensions,
        };
    }

    private async Task<WcProductDto?> FetchProductAsync(HttpClient client, string authQs, string productId, CancellationToken ct)
    {
        try
        {
            using var resp = await client
                .GetAsync($"wp-json/wc/v3/products/{Uri.EscapeDataString(productId)}{authQs}", ct)
                .ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                log.LogWarning("WC product fetch {ProductId} returned {Status}", productId, (int)resp.StatusCode);
                return null;
            }
            return await resp.Content.ReadFromJsonAsync<WcProductDto>(JsonOptions, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            log.LogWarning(ex, "WC product fetch {ProductId} failed", productId);
            return null;
        }
    }

    private static int ParseProductWeightGrams(string? rawWeight)
    {
        // WC stores weight in the configured store unit (kg by default for European stores).
        // The REST API returns the raw stored value as a string; "" / "0" both mean "unset".
        if (string.IsNullOrWhiteSpace(rawWeight)) return 0;
        if (!decimal.TryParse(rawWeight, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var kg))
            return 0;
        return (int)Math.Round(kg * 1000m);
    }

    private static bool HasUsableDimensions(WcProductDimensionsDto? d) =>
        d is not null
        && (decimal.TryParse(d.Length, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var l) && l > 0)
        && (decimal.TryParse(d.Width, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var w) && w > 0)
        && (decimal.TryParse(d.Height, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var h) && h > 0);

    private static Domain.ValueObjects.Dimension ToDimension(WcProductDimensionsDto d)
    {
        // WC stores dimensions in cm by default; the orchestrator's Dimension is in mm.
        var l = decimal.Parse(d.Length!, System.Globalization.CultureInfo.InvariantCulture);
        var w = decimal.Parse(d.Width!, System.Globalization.CultureInfo.InvariantCulture);
        var h = decimal.Parse(d.Height!, System.Globalization.CultureInfo.InvariantCulture);
        return new Domain.ValueObjects.Dimension(
            LengthMm: (int)Math.Round(l * 10m),
            WidthMm: (int)Math.Round(w * 10m),
            HeightMm: (int)Math.Round(h * 10m));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private sealed record WcProductDto(
        long? Id,
        string? Weight,
        WcProductDimensionsDto? Dimensions);

    private sealed record WcProductDimensionsDto(string? Length, string? Width, string? Height);

    public Task<WebhookHandled> HandleWebhookAsync(RawWebhook webhook, CancellationToken ct)
    {
        // Returning true here doesn't mean the orchestrator skips HMAC validation — the webhook
        // endpoint still verifies the signature using the per-connection webhook secret. This
        // method is the contract hook the application layer can use for connector-side custom
        // validation; for WC the secret is per-connection (not per-app), so validation lives at
        // the endpoint where the connection record is in scope.
        return Task.FromResult(new WebhookHandled(true));
    }

    public Task PushFulfillmentAsync(TenantId tenantId, FulfillmentUpdate update, CancellationToken ct)
    {
        log.LogInformation("WooCommerce push fulfillment for tenant {Tenant}, order {OrderId}, tracking {Tracking}",
            tenantId, update.ExternalOrderId, update.TrackingNumber);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Pulls a single order from the WC REST API for the Recheck flow. The webhook delivers
    /// the order object directly, and the REST endpoint returns the same shape, so the body
    /// is passed through to <see cref="WooCommerceOrderTranslator"/> unchanged.
    /// </summary>
    public async Task<OrderFetchResult> FetchRawOrderAsync(OrderFetchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.DecryptedCredentials.Length == 0)
            return new OrderFetchResult(false, null, "no credentials available for this connection");

        WooCommerceCredentialsBundle? bundle;
        try
        {
            bundle = JsonSerializer.Deserialize<WooCommerceCredentialsBundle>(request.DecryptedCredentials);
        }
        catch (JsonException ex)
        {
            log.LogWarning(ex, "WooCommerce credentials bundle could not be parsed for store {Store}.", request.ExternalAccountId);
            return new OrderFetchResult(false, null, "stored credentials could not be parsed");
        }
        if (bundle is null) return new OrderFetchResult(false, null, "credentials bundle was empty");

        // Use the StoreUrl from the bundle rather than ExternalAccountId — they are normally
        // identical (FindByPlatformAccountAsync key) but the bundle is the source of truth
        // and survives platform-side renames.
        using var client = httpFactory.CreateClient(HttpClientName);
        client.BaseAddress = new Uri(bundle.StoreUrl.TrimEnd('/') + "/");

        var qs = $"?consumer_key={Uri.EscapeDataString(bundle.ConsumerKey)}&consumer_secret={Uri.EscapeDataString(bundle.ConsumerSecret)}";
        var path = $"wp-json/wc/v3/orders/{Uri.EscapeDataString(request.ExternalOrderId)}{qs}";

        try
        {
            using var resp = await client.GetAsync(path, cancellationToken).ConfigureAwait(false);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return new OrderFetchResult(false, null, $"WooCommerce returned 404 for order {request.ExternalOrderId}.");
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                log.LogWarning("WooCommerce order fetch {OrderId} on {Store} returned {Status}: {Body}",
                    request.ExternalOrderId, bundle.StoreUrl, (int)resp.StatusCode, err);
                return new OrderFetchResult(false, null, $"WooCommerce returned {(int)resp.StatusCode}.");
            }

            var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new OrderFetchResult(true, json, null);
        }
        catch (HttpRequestException ex)
        {
            log.LogWarning(ex, "WooCommerce order fetch {OrderId} on {Store} failed.",
                request.ExternalOrderId, bundle.StoreUrl);
            return new OrderFetchResult(false, null, $"Could not reach WooCommerce: {ex.Message}");
        }
    }

    public static bool TryValidateWebhookSignature(byte[] rawBody, string providedBase64, string webhookSecret)
    {
        if (string.IsNullOrEmpty(providedBase64) || string.IsNullOrEmpty(webhookSecret)) return false;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
        var computed = Convert.ToBase64String(hmac.ComputeHash(rawBody));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(providedBase64),
            Encoding.UTF8.GetBytes(computed));
    }

    private async Task RegisterWebhooksAsync(string storeUrl, string consumerKey, string consumerSecret, string webhookSecret, CancellationToken ct)
    {
        using var client = httpFactory.CreateClient(HttpClientName);
        client.BaseAddress = new Uri(storeUrl);

        // Query-string auth instead of HTTP Basic. The wordpress:apache image doesn't
        // forward the `Authorization` header to mod_php by default (no AllowOverride for
        // Authorization in the bundled .htaccess), so Basic auth silently 401s. WC supports
        // ?consumer_key=&consumer_secret= as an equivalent auth method on REST endpoints.
        var qs = $"?consumer_key={Uri.EscapeDataString(consumerKey)}&consumer_secret={Uri.EscapeDataString(consumerSecret)}";

        await PurgeOrchestratorWebhooksAsync(client, qs, ct).ConfigureAwait(false);

        foreach (var topic in new[] { "order.created", "order.updated" })
        {
            var body = new
            {
                name = $"Ship Shipping ({topic})",
                topic,
                delivery_url = _options.OrchestratorWebhookUrl,
                secret = webhookSecret,
                status = "active",
            };
            using var resp = await client.PostAsJsonAsync($"wp-json/wc/v3/webhooks{qs}", body, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                var errorBody = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                throw new HttpRequestException(
                    $"WC webhook registration for topic '{topic}' returned {(int)resp.StatusCode}: {errorBody}");
            }
        }
        log.LogInformation("Registered WooCommerce webhooks for {Store}", storeUrl);
    }

    /// <summary>
    /// Name prefix every webhook we register starts with. Used to identify our own rows on
    /// re-install for purge — robust to <see cref="WooCommerceOptions.OrchestratorWebhookUrl"/>
    /// changing between installs (e.g. switching from HTTPS:5111 to HTTP:5101 dev profiles),
    /// where matching purely on delivery URL would orphan the old hooks forever.
    /// </summary>
    public const string WebhookNamePrefix = "Ship Shipping (";

    /// <summary>
    /// Deletes any pre-existing WC webhooks created by this connector — identified by name
    /// prefix, NOT delivery URL, so a config change between installs still cleans up old rows.
    /// WC stores webhooks as plain rows with no uniqueness on (delivery_url, topic), so re-install
    /// would otherwise create duplicates that fan-out to N copies of every order event. Cleanup
    /// is best-effort: a failure here only logs and proceeds, since the duplicate is a nuisance
    /// rather than a correctness break (the inbound endpoint is idempotent on order id).
    /// </summary>
    private async Task PurgeOrchestratorWebhooksAsync(HttpClient client, string authQs, CancellationToken ct)
    {
        var listUrl = $"wp-json/wc/v3/webhooks{authQs}&per_page=100";
        using var listResp = await client.GetAsync(listUrl, ct).ConfigureAwait(false);
        if (!listResp.IsSuccessStatusCode)
        {
            log.LogWarning("Could not list existing WC webhooks for cleanup ({Status}); skipping purge.",
                (int)listResp.StatusCode);
            return;
        }

        var existing = await listResp.Content
            .ReadFromJsonAsync<List<WcWebhookSummary>>(cancellationToken: ct)
            .ConfigureAwait(false) ?? [];

        var stale = existing
            .Where(h => h.Name?.StartsWith(WebhookNamePrefix, StringComparison.Ordinal) == true)
            .ToArray();
        if (stale.Length == 0) return;

        foreach (var hook in stale)
        {
            // `force=true` skips the trash bin so re-installs see a clean slate.
            using var del = await client.DeleteAsync(
                $"wp-json/wc/v3/webhooks/{hook.Id}{authQs}&force=true", ct).ConfigureAwait(false);
            if (del.IsSuccessStatusCode)
                log.LogInformation("Removed stale WC webhook id={Id} name='{Name}' delivery={Url}",
                    hook.Id, hook.Name, hook.DeliveryUrl);
            else
                log.LogWarning("Failed to delete stale WC webhook id={Id} name='{Name}': HTTP {Status}",
                    hook.Id, hook.Name, (int)del.StatusCode);
        }
    }

    private sealed record WcWebhookSummary(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("delivery_url")] string DeliveryUrl);

    private static string GenerateWebhookSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }
}

/// <summary>Persisted (encrypted) credential bundle for a WC connection. Read by the webhook endpoint to verify HMACs.</summary>
public sealed record WooCommerceCredentialsBundle(
    [property: JsonPropertyName("consumer_key")] string ConsumerKey,
    [property: JsonPropertyName("consumer_secret")] string ConsumerSecret,
    [property: JsonPropertyName("store_url")] string StoreUrl,
    [property: JsonPropertyName("webhook_secret")] string WebhookSecret);
