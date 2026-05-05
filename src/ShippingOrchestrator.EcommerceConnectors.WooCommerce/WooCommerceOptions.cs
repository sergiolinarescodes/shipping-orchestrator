namespace ShippingOrchestrator.EcommerceConnectors.WooCommerce;

public sealed class WooCommerceOptions
{
    public const string SectionName = "Connectors:WooCommerce";

    /// <summary>Shown to the WP store owner on the WC Authentication Endpoint approval screen.</summary>
    public string AppName { get; set; } = "Ship Shipping";

    /// <summary>WC scope: <c>read</c>, <c>write</c>, or <c>read_write</c>. Webhook auto-registration needs <c>read_write</c>.</summary>
    public string Scope { get; set; } = "read_write";

    /// <summary>Where the merchant's browser is redirected after they Approve in their wp-admin.</summary>
    public string ReturnUrl { get; set; } = "http://localhost:5173/connections?wc=connected";

    /// <summary>Where WP POSTs the consumer key/secret after the merchant approves.</summary>
    public string CallbackBaseUrl { get; set; } = "http://localhost:5101";

    /// <summary>URL we register with WC for webhook delivery (must be reachable from the WP container).</summary>
    public string OrchestratorWebhookUrl { get; set; } = "http://host.docker.internal:5101/v1/webhooks/woocommerce";

    /// <summary>Override the WP host (e.g. for a local WireMock stub in tests).</summary>
    public string? AuthorityOverride { get; set; }

    /// <summary>Base URL the in-memory connector points install callbacks at. Only used when <c>Mode=InMemory</c>.</summary>
    public string OnboardingCallbackBaseUrl { get; set; } = "http://localhost:5101";
}
