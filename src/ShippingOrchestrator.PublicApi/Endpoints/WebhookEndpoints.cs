using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Encryption;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Application.Connections;
using ShippingOrchestrator.Application.Ingestion;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.EcommerceConnectors.Shopify;
using ShippingOrchestrator.EcommerceConnectors.WooCommerce;
using ShippingOrchestrator.Modules.Abstractions;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;
using ShippingOrchestrator.PublicApi.Realtime;
using ShippingOrchestrator.ReadModels.Realtime;
using Wolverine;
using DomainReason = ShippingOrchestrator.Domain.Ingestion.IngestionReasonCode;

namespace ShippingOrchestrator.PublicApi.Endpoints;

/// <summary>
/// Inbound webhook surface for ecommerce connectors. Each platform's webhook posts here; the
/// endpoint resolves the per-tenant <c>EcommerceConnection</c> by shop domain (or equivalent
/// header), hands the raw body to the connector-specific translator, and dispatches the
/// resulting normalized order via <see cref="IngestEcommerceOrderCommand"/> — the same path
/// the admin simulator uses, so production and dev share identical downstream behaviour.
///
/// Tenant security: webhooks are anonymous from the network's POV; the trust boundary is
/// (a) HMAC validation against the platform's per-app or per-webhook secret, and (b) deriving
/// the tenant id from the persisted connection rather than any caller-supplied header. A
/// caller cannot point a webhook at a different tenant's data without forging both the HMAC
/// and the platform/account pair, which the unique index already enforces is one-to-one.
/// </summary>
public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/webhooks").WithTags("Webhooks");

        group.MapPost("/shopify", async (
            HttpContext http,
            IEcommerceConnectionRepository connections,
            IEcommerceOrderTranslatorRegistry translators,
            IOptionsMonitor<ShopifyOptions> shopifyOptions,
            IOptionsMonitor<ConnectorModeOptions> connectorModeOptions,
            ILoggerFactory loggerFactory,
            IMessageBus bus,
            IIngestionDispatcher dispatcher,
            IRealtimeNotifier notifier,
            IRawBodyRedactor redactor,
            IClock clock,
            [FromServices] IRateLimiter rateLimiter,
            CancellationToken ct) =>
        {
            var log = loggerFactory.CreateLogger("ShopifyWebhook");

            using var ms = new MemoryStream();
            await http.Request.Body.CopyToAsync(ms, ct).ConfigureAwait(false);
            var rawBody = ms.ToArray();
            var body = Encoding.UTF8.GetString(rawBody);

            var shopDomain = http.Request.Headers.TryGetValue("X-Shopify-Shop-Domain", out var v) ? v.ToString() : null;
            if (string.IsNullOrWhiteSpace(shopDomain))
                return Results.BadRequest(new { error = "X-Shopify-Shop-Domain header is required." });

            var topic = http.Request.Headers.TryGetValue("X-Shopify-Topic", out var t) ? t.ToString() : null;

            var mode = connectorModeOptions.Get("shopify").Mode;
            if (mode == ConnectorMode.Real)
            {
                if (!TryValidateShopifyHmac(http.Request.Headers, rawBody, shopifyOptions.CurrentValue.ClientSecret, out var hmacError))
                {
                    log.LogWarning("Shopify webhook HMAC rejected for shop {Shop}: {Reason}", shopDomain, hmacError);
                    return Results.Unauthorized();
                }
            }
            else
            {
                log.LogDebug("Shopify webhook HMAC skipped (mode={Mode}) for shop {Shop}", mode, shopDomain);
            }

            var connection = await connections.FindByPlatformAccountAsync("shopify", shopDomain, ct).ConfigureAwait(false);
            if (connection is null)
                return Results.NotFound(new { error = $"No tenant connection registered for shop '{shopDomain}'." });

            // Per-tenant token bucket. Acquire AFTER HMAC + connection lookup so unverified
            // traffic can never claim a partition slot, but BEFORE we dispatch any work that
            // would charge SQS/Postgres on the tenant's behalf. Lifecycle (app/uninstalled)
            // bypasses the bucket — uninstall must succeed even under rate-limit pressure.
            if (!string.Equals(topic, "app/uninstalled", StringComparison.OrdinalIgnoreCase))
            {
                // Per-(connector, tenant) cap matches the platform's per-shop ceiling. A future
                // custom-API order path that should share a tenant-level cap with webhooks would
                // additionally acquire RateLimitPartitions.Tenant(tenantId) and reject if either
                // bucket denies — see RateLimitPartitions for the dual-acquire pattern.
                using var lease = await rateLimiter
                    .AcquireAsync(RateLimitPartitions.Connector("shopify", connection.TenantId.Value), ct)
                    .ConfigureAwait(false);
                if (!lease.IsAcquired)
                {
                    log.LogWarning("Shopify webhook rate-limited for tenant {TenantId} shop {Shop}",
                        connection.TenantId.Value, shopDomain);
                    return RateLimitedResult(http, lease);
                }
            }

            // Lifecycle topics never produce orders. Branch BEFORE translator dispatch so a
            // missing translator can't 501 a perfectly valid uninstall event.
            if (string.Equals(topic, "app/uninstalled", StringComparison.OrdinalIgnoreCase))
            {
                await bus.InvokeAsync<DisconnectEcommerceConnectionResult>(
                    new DisconnectEcommerceConnectionCommand(connection.Id, connection.TenantId, "shopify app/uninstalled webhook"),
                    ct).ConfigureAwait(false);
                log.LogInformation("Shopify connection {ConnectionId} removed via app/uninstalled", connection.Id);
                return Results.Ok(new { status = "removed" });
            }

            var headers = http.Request.Headers
                .ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);

            if (!translators.TryResolve("shopify", out var translator) || translator is null)
                return Results.Problem(
                    title: "No translator registered for connector 'shopify'.",
                    statusCode: StatusCodes.Status501NotImplemented);

            return await TranslateOrRecordAsync(
                translator, enricher: null, decryptedCredentials: null,
                bus, dispatcher, notifier, redactor,
                connection.TenantId, "shopify", shopDomain,
                body, rawBody, headers, log, ct).ConfigureAwait(false);
        }).AllowAnonymous().WithName("ShopifyWebhook");

        // WooCommerce webhook. WP signs each delivery with HMAC-SHA256 of the raw body using
        // the per-webhook secret stored in the EcommerceConnection's credentials cipher. We
        // resolve the connection by store URL (X-WC-Webhook-Source header), decrypt the
        // bundle, validate the signature, then translate + ingest. The `topic` header
        // (e.g. order.created) tells us what shape the body has.
        group.MapPost("/woocommerce", async (
            HttpContext http,
            IEcommerceConnectionRepository connections,
            IEcommerceOrderTranslatorRegistry translators,
            IEnvelopeEncryptor encryptor,
            IOptionsMonitor<ConnectorModeOptions> connectorModeOptions,
            ILoggerFactory loggerFactory,
            IMessageBus bus,
            IIngestionDispatcher dispatcher,
            IRealtimeNotifier notifier,
            IRawBodyRedactor redactor,
            ConnectorRegistry connectorRegistry,
            IServiceProvider services,
            [FromServices] IRateLimiter rateLimiter,
            CancellationToken ct) =>
        {
            var log = loggerFactory.CreateLogger("WooCommerceWebhook");

            using var ms = new MemoryStream();
            await http.Request.Body.CopyToAsync(ms, ct).ConfigureAwait(false);
            var rawBody = ms.ToArray();
            var body = Encoding.UTF8.GetString(rawBody);

            // WC pings each webhook on creation (`WC_Webhook::deliver_ping`) with body
            // `webhook_id=N` urlencoded and no `X-WC-Webhook-*` headers. A non-2xx response
            // counts toward WC's 5-strike auto-disable, so we ack pings explicitly with 200
            // BEFORE the source-header guard rejects them. Real deliveries always carry the
            // Source header; pings never do — that distinction is the discriminator.
            var contentType = http.Request.ContentType ?? string.Empty;
            if (contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
                && Encoding.UTF8.GetString(rawBody).StartsWith("webhook_id=", StringComparison.Ordinal))
            {
                log.LogInformation("WooCommerce webhook ping ack (body={Body})", Encoding.UTF8.GetString(rawBody));
                return Results.Ok(new { status = "ping_ack" });
            }

            var sourceHeader = http.Request.Headers.TryGetValue("X-WC-Webhook-Source", out var src) ? src.ToString() : null;
            if (string.IsNullOrWhiteSpace(sourceHeader))
                return Results.BadRequest(new { error = "X-WC-Webhook-Source header is required." });

            // Same canonicalization applied at install-time (lowercase host, no trailing slash)
            // so the lookup key matches the row that CompleteEcommerceOAuthHandler stored.
            var storeUrl = WooCommerceEcommerceConnector.NormalizeStoreUrl(sourceHeader);
            var topic = http.Request.Headers.TryGetValue("X-WC-Webhook-Topic", out var t) ? t.ToString() : "order.created";
            var providedSig = http.Request.Headers.TryGetValue("X-WC-Webhook-Signature", out var s) ? s.ToString() : null;

            var connection = await connections.FindByPlatformAccountAsync("woocommerce", storeUrl, ct).ConfigureAwait(false);
            if (connection is null)
                return Results.NotFound(new { error = $"No tenant connection registered for store '{storeUrl}'." });

            var bundleBytes = await encryptor.DecryptAsync(connection.CredentialsCipher, ct).ConfigureAwait(false);
            var bundle = System.Text.Json.JsonSerializer.Deserialize<WooCommerceCredentialsBundle>(bundleBytes)
                ?? throw new InvalidOperationException($"WooCommerce credentials bundle for connection {connection.Id} could not be parsed.");

            var mode = connectorModeOptions.Get("woocommerce").Mode;
            if (mode == ConnectorMode.Real)
            {
                if (string.IsNullOrEmpty(providedSig)
                    || !WooCommerceEcommerceConnector.TryValidateWebhookSignature(rawBody, providedSig, bundle.WebhookSecret))
                {
                    log.LogWarning("WooCommerce webhook HMAC rejected for store {Store}", storeUrl);
                    return Results.Unauthorized();
                }
            }
            else
            {
                log.LogDebug("WooCommerce webhook HMAC skipped (mode={Mode}) for store {Store}", mode, storeUrl);
            }

            // Topics other than order events are accepted but produce no pending order. Keeps
            // the contract uniform — WP retries any non-2xx, so silent-accept is safer than 501.
            if (!topic.StartsWith("order.", StringComparison.OrdinalIgnoreCase))
            {
                log.LogInformation("WooCommerce webhook accepted (no-op): topic={Topic} store={Store}", topic, storeUrl);
                return Results.Ok(new { status = "ignored", topic });
            }

            // Rate limit only the order.* path — the SQS-write hot path. Non-order topics
            // already short-circuit above with 200 and never charge any downstream resource.
            using (var lease = await rateLimiter
                .AcquireAsync(RateLimitPartitions.Connector("woocommerce", connection.TenantId.Value), ct)
                .ConfigureAwait(false))
            {
                if (!lease.IsAcquired)
                {
                    log.LogWarning("WooCommerce webhook rate-limited for tenant {TenantId} store {Store}",
                        connection.TenantId.Value, storeUrl);
                    return RateLimitedResult(http, lease);
                }
            }

            var headers = http.Request.Headers
                .ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);

            if (!translators.TryResolve("woocommerce", out var translator) || translator is null)
                return Results.Problem(
                    title: "No translator registered for connector 'woocommerce'.",
                    statusCode: StatusCodes.Status501NotImplemented);

            // Pull the connector instance so the WC-specific enricher can fetch product weights
            // (the order webhook payload does NOT carry per-line weight — that lives on the
            // product). For platforms whose webhook is self-sufficient (Shopify), this resolves
            // to a connector that doesn't implement IEcommerceOrderEnricher and is skipped.
            var enricher = connectorRegistry.TryGet("woocommerce", out var wcReg) && wcReg is not null
                ? wcReg.ConnectorFactory(services) as IEcommerceOrderEnricher
                : null;

            return await TranslateOrRecordAsync(
                translator, enricher, decryptedCredentials: bundleBytes,
                bus, dispatcher, notifier, redactor,
                connection.TenantId, "woocommerce", storeUrl,
                body, rawBody, headers, log, ct).ConfigureAwait(false);
        }).AllowAnonymous().WithName("WooCommerceWebhook");
    }

    /// <summary>
    /// Single translate-and-record path used by every ecommerce webhook branch. Translator
    /// success → 202 + pending-order id. Connector throws <see cref="IngestionTranslationException"/>
    /// → 200 + recorded exception (the persisted record is what the customer SPA "Needs
    /// attention" page reads). Anything else → 200 + recorded ParseError row, so the platform
    /// never retries silently and ops can spot translator regressions in the aggregated view.
    /// </summary>
    private static async Task<IResult> TranslateOrRecordAsync(
        IEcommerceOrderTranslator translator,
        IEcommerceOrderEnricher? enricher,
        byte[]? decryptedCredentials,
        IMessageBus bus,
        IIngestionDispatcher dispatcher,
        IRealtimeNotifier notifier,
        IRawBodyRedactor redactor,
        TenantId tenantId,
        string connectorCode,
        string externalAccountId,
        string body,
        byte[] rawBody,
        IReadOnlyDictionary<string, string> headers,
        ILogger log,
        CancellationToken ct)
    {
        try
        {
            var payload = await translator
                .TranslateAsync(tenantId, externalAccountId, body, headers, ct)
                .ConfigureAwait(false);
            if (enricher is not null && decryptedCredentials is not null)
            {
                payload = await enricher.EnrichAsync(payload, decryptedCredentials, ct).ConfigureAwait(false);
            }
            // Post-translate validation: connectors whose payloads can land here without weight
            // (WooCommerce — order shape doesn't carry it inline) rely on the enricher above to
            // fill it in. If we still hit zero after both, it's the genuine "tenant hasn't set
            // a product weight" case the dashboard's Recheck flow is built around.
            if (payload.TotalWeight.Grams <= 0)
            {
                throw new IngestionTranslationException(
                    IngestionReasonCode.ZeroWeight,
                    connectorCode,
                    payload.ExternalOrderId,
                    "Set a weight on the product (Products → Edit → Shipping tab → Weight). " +
                    "Existing orders snapshot the weight at creation time, so after fixing the product click Recheck here " +
                    "to re-pull this order — or open the order in the platform admin and update it to refresh the snapshot.",
                    $"{connectorCode} order has zero total weight across line items after enrichment.");
            }
            var ingest = await dispatcher.DispatchAsync(payload, ct).ConfigureAwait(false);
            await notifier.NotifyDashboardAsync(
                    tenantId.Value, DashboardEvents.Invalidate, new { area = DashboardArea.Orders }, ct)
                .ConfigureAwait(false);
            return Results.Accepted(
                $"/v1/dashboard/orders/pending/{ingest.PendingOrderId}",
                new { pendingOrderId = ingest.PendingOrderId });
        }
        catch (IngestionTranslationException ex)
        {
            var record = await bus.InvokeAsync<RecordIngestionFailureResult>(
                new RecordIngestionFailureCommand(
                    tenantId,
                    connectorCode,
                    ex.ExternalOrderId,
                    (DomainReason)(int)ex.Code,
                    ex.Message,
                    ex.TenantHint,
                    redactor.Redact(body),
                    redactor.Hash(rawBody),
                    ex.Context),
                ct).ConfigureAwait(false);
            log.LogWarning(
                "Recorded ingestion failure {FailureId} for {Connector}/{ExternalOrderId}: {Reason}",
                record.FailureId, connectorCode, ex.ExternalOrderId, ex.Code);
            return Results.Ok(new
            {
                status = "recorded_failure",
                reasonCode = ex.Code.ToString(),
                failureId = record.FailureId,
                hint = ex.TenantHint,
            });
        }
        catch (Exception ex)
        {
            var record = await bus.InvokeAsync<RecordIngestionFailureResult>(
                new RecordIngestionFailureCommand(
                    tenantId,
                    connectorCode,
                    ExternalOrderId: null,
                    DomainReason.ParseError,
                    ex.Message,
                    "We couldn't read this order. Contact support if it keeps happening.",
                    redactor.Redact(body),
                    redactor.Hash(rawBody),
                    Context: null),
                ct).ConfigureAwait(false);
            log.LogError(
                ex, "Recorded parse-error ingestion failure {FailureId} for {Connector}",
                record.FailureId, connectorCode);
            return Results.Ok(new
            {
                status = "recorded_failure",
                reasonCode = "ParseError",
                failureId = record.FailureId,
            });
        }
    }

    private static IResult RateLimitedResult(HttpContext http, System.Threading.RateLimiting.RateLimitLease lease)
    {
        if (lease.TryGetMetadata(System.Threading.RateLimiting.MetadataName.RetryAfter, out var retryAfter))
        {
            // Surface Retry-After so the platform's own retry policy honors our budget rather
            // than hammering us through backoff. Shopify and WooCommerce both respect it.
            http.Response.Headers["Retry-After"] =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }
        return Results.Json(
            new { status = "rate_limited", error = "Per-tenant webhook rate limit exceeded." },
            statusCode: StatusCodes.Status429TooManyRequests);
    }

    private static bool TryValidateShopifyHmac(IHeaderDictionary headers, byte[] rawBody, string secret, out string? error)
    {
        error = null;
        if (string.IsNullOrEmpty(secret))
        {
            error = "Shopify ClientSecret is not configured.";
            return false;
        }
        if (!headers.TryGetValue("X-Shopify-Hmac-Sha256", out var headerValue))
        {
            error = "Missing X-Shopify-Hmac-Sha256 header.";
            return false;
        }
        var provided = headerValue.ToString();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computed = Convert.ToBase64String(hmac.ComputeHash(rawBody));
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(provided),
                Encoding.UTF8.GetBytes(computed)))
        {
            error = "HMAC mismatch.";
            return false;
        }
        return true;
    }
}
