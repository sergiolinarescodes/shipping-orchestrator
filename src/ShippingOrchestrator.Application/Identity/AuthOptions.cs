namespace ShippingOrchestrator.Application.Identity;

/// <summary>
/// Tunables for the magic-link auth flow. Bound from configuration section <c>Auth</c>.
/// Defaults match production-sane values; tests override with shorter TTLs.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Absolute base URL of the PublicApi host that serves <c>/v1/auth/verify</c>. Used to
    /// build the link mailed to the user. Must be reachable from the user's browser, not
    /// just the server (e.g. <c>https://api.example.com</c> in prod, <c>http://localhost:5101</c>
    /// in dev).
    /// </summary>
    public string VerifyEndpointBaseUrl { get; set; } = "http://localhost:5101";

    /// <summary>
    /// Absolute base URL of the customer SPA. The verify endpoint redirects here after
    /// setting the session cookie (e.g. <c>http://localhost:5173</c>).
    /// </summary>
    public string DashboardBaseUrl { get; set; } = "http://localhost:5173";

    public int MagicLinkTtlSeconds { get; set; } = 900;

    public int SessionTtlSeconds { get; set; } = 60 * 60 * 24 * 30;

    public string FromAddress { get; set; } = "no-reply@shipping-orchestrator.local";

    public string FromDisplayName { get; set; } = "Shipping Orchestrator";

    /// <summary>
    /// Cookie name. Use <c>__Host-session</c> in production (forces Secure + Path=/ + no
    /// Domain). In Development we drop the prefix so it works on plain HTTP localhost.
    /// </summary>
    public string CookieName { get; set; } = "so.session";
}
