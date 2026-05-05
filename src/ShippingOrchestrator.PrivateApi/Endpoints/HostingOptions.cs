namespace ShippingOrchestrator.PrivateApi.Endpoints;

/// <summary>
/// Bound from the <c>Hosting</c> section. Used by ops endpoints that need to hand a customer
/// dashboard URL back to the operator after creating a tenant. Set per-environment so the
/// same code path produces the right URL locally and in production.
/// </summary>
public sealed class HostingOptions
{
    public const string SectionName = "Hosting";

    /// <summary>
    /// Origin (scheme + host[:port]) of the customer SPA. The operator copies the resulting
    /// "<c>{base}/login?tenant={id}</c>" URL out of the wizard handoff screen and emails it
    /// to the new tenant.
    /// </summary>
    public string CustomerDashboardBaseUrl { get; set; } = "http://localhost:5173";
}
