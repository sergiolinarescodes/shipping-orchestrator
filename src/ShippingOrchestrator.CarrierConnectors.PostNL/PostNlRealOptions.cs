namespace ShippingOrchestrator.CarrierConnectors.PostNL;

/// <summary>
/// Configuration for the production PostNL HTTP integration.
/// Bound from <c>Connectors:PostNL:Real</c>. Currently unused — the real adapter is not
/// implemented yet — but the shape is fixed here so the module wires it consistently.
/// </summary>
public sealed class PostNlRealOptions
{
    /// <summary>PostNL API base URL (per environment: sandbox vs prod).</summary>
    public string ApiBaseUrl { get; set; } = "https://api.postnl.nl";

    /// <summary>API key fetched from Secrets Manager / KMS in real deployments.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Per-request HTTP timeout. Defaults to 10s.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
