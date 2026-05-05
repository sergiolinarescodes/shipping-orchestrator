using Microsoft.AspNetCore.SignalR;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.PublicApi.Realtime;

/// <summary>
/// Tenant-scoped push channel for the customer dashboard. PublicApi-resident handlers
/// (webhook intake) push "dashboard:invalidate" events here so SPAs can drop their polling
/// intervals and react in real time. Worker-resident projections reach the same hub via the
/// <c>BroadcastDashboardEvent</c> Wolverine hop — an event published from a projection
/// handler is consumed by <c>BroadcastDashboardHandler</c> on a PublicApi pod and fanned
/// out to subscribed clients.
/// </summary>
public sealed class RealtimeHub : Hub
{
    private readonly ITenantRepository _tenants;

    public RealtimeHub(ITenantRepository tenants)
    {
        _tenants = tenants;
    }

    /// <summary>
    /// SignalR clients connect anonymously (cookies/JWT for the customer dashboard are not
    /// portable to WebSocket from a browser without query-string ferrying), then call this
    /// method with the tenant id from their session. The id is validated against the
    /// orchestrator.tenants table before the connection joins the tenant group, so a hostile
    /// caller cannot subscribe to another tenant's events without first knowing a real id.
    /// </summary>
    public async Task SubscribeTenant(Guid tenantId)
    {
        var tenant = await _tenants.FindAsync(new TenantId(tenantId), Context.ConnectionAborted)
            .ConfigureAwait(false);
        if (tenant is null)
            throw new HubException($"Tenant {tenantId} does not exist.");
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(tenantId), Context.ConnectionAborted)
            .ConfigureAwait(false);
    }

    public Task UnsubscribeTenant(Guid tenantId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(tenantId), Context.ConnectionAborted);

    internal static string GroupName(Guid tenantId)
        => "tenant:" + tenantId.ToString("D", System.Globalization.CultureInfo.InvariantCulture);
}
