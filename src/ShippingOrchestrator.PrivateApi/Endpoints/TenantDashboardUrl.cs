namespace ShippingOrchestrator.PrivateApi.Endpoints;

/// <summary>
/// Single source of truth for the customer-dashboard URL we hand back to ops after creating a
/// tenant. Kept in one helper so both <c>POST /admin/tenants</c> and the onboarding-process
/// view return the same shape, and any future routing change (path, query key, deep-link to
/// connections) is one edit away.
/// </summary>
internal static class TenantDashboardUrl
{
    public static string Build(string baseUrl, Guid tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        var trimmed = baseUrl.TrimEnd('/');
        return $"{trimmed}/login?tenant={tenantId}";
    }
}
