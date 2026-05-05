namespace ShippingOrchestrator.EcommerceConnectors.Shopify;

public sealed class ShopifyOptions
{
    public const string SectionName = "Connectors:Shopify";

    /// <summary>Shopify Partners "API key".</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Shopify Partners "API secret key" — kept in Secrets Manager in production.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Comma-separated scopes (e.g. <c>read_orders,write_fulfillments</c>).</summary>
    public string Scopes { get; set; } = "read_orders,write_fulfillments";

    /// <summary>Override the Shopify host (e.g. for a local WireMock stub in tests).</summary>
    public string? AuthorityOverride { get; set; }

    /// <summary>
    /// Public-facing URL where Shopify dispatches webhooks. Each store install programmatically
    /// registers <c>orders/create</c>, <c>orders/paid</c>, <c>fulfillments/create</c>, and
    /// <c>app/uninstalled</c> against this address. Required in <c>Mode=Real</c> — Shopify
    /// servers can't reach <c>localhost</c>, so set to a tunnel URL (cloudflared / ngrok) in
    /// dev or to your public API host in production. The orchestrator's webhook endpoint is
    /// <c>/v1/webhooks/shopify</c> — set this to <c>https://your-public-host</c> (no path).
    /// </summary>
    public string? OrchestratorWebhookBaseUrl { get; set; }

    /// <summary>Shopify Admin API version used for the webhook registration calls.</summary>
    public string ApiVersion { get; set; } = "2026-07";
}
