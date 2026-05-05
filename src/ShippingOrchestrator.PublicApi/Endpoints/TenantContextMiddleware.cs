using System.Security.Claims;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Infrastructure.Persistence.Tenancy;

namespace ShippingOrchestrator.PublicApi.Endpoints;

/// <summary>
/// Reads the tenant id from the authenticated principal and pushes it into the ambient
/// tenant context for the duration of the request. Anonymous endpoints (OAuth callback,
/// install URL) skip this; they accept the tenant id as a request parameter.
/// </summary>
public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context)
    {
        var tenantClaim = context.User.FindFirstValue("tenant_id")
            ?? context.User.FindFirstValue("https://shipping-orchestrator/tenant_id");

        if (Guid.TryParse(tenantClaim, out var tenantGuid))
        {
            using var _ = AmbientTenantContext.Set(new TenantId(tenantGuid));
            await next(context).ConfigureAwait(false);
        }
        else
        {
            await next(context).ConfigureAwait(false);
        }
    }
}
