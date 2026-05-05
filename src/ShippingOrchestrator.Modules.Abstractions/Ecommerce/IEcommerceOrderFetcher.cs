namespace ShippingOrchestrator.Modules.Abstractions.Ecommerce;

/// <summary>
/// Optional capability for an <see cref="IEcommerceConnector"/>: fetch a single order from
/// the platform on demand. Used by the "Recheck" action on ingestion failures — the tenant
/// fixes the underlying issue (product weight, address, …) in their store and the
/// orchestrator pulls the fresh order, re-runs translation, and resolves the failure
/// without the tenant having to manually re-save the order to retrigger a webhook.
///
/// Connectors opt in by implementing this interface alongside <see cref="IEcommerceConnector"/>.
/// The Application layer probes via <c>connector as IEcommerceOrderFetcher</c>; if not
/// implemented, the Recheck endpoint returns a clear "not supported by this connector"
/// response rather than failing.
/// </summary>
public interface IEcommerceOrderFetcher
{
    /// <summary>
    /// Pull the order's current state from the platform's REST API and return the raw
    /// JSON body in the same shape <see cref="IEcommerceOrderTranslator.TranslateAsync"/>
    /// already expects (i.e. the same body the platform would deliver in an
    /// <c>orders/updated</c> webhook). The translator should run unchanged on the result.
    /// </summary>
    Task<OrderFetchResult> FetchRawOrderAsync(OrderFetchRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Inputs for an on-demand order fetch.
/// <para>
/// <see cref="DecryptedCredentials"/> is the same byte[] the OAuth flow produced (raw access
/// token bytes for Shopify, JSON-serialized credentials bundle for WooCommerce). The caller
/// is responsible for envelope-decrypting the connection's <c>CredentialsCipher</c> before
/// invoking the fetcher — connectors don't depend on Application or Infrastructure.
/// </para>
/// </summary>
public sealed record OrderFetchRequest(
    string ExternalAccountId,
    string ExternalOrderId,
    byte[] DecryptedCredentials);

/// <summary>
/// Result of a fetch attempt. <see cref="Success"/> = false carries a tenant-readable reason
/// the Recheck endpoint surfaces back to the caller (e.g. "order 19 not found in store",
/// "store unreachable"). The body is null in that case.
/// </summary>
public sealed record OrderFetchResult(
    bool Success,
    string? RawBody,
    string? FailureReason);
