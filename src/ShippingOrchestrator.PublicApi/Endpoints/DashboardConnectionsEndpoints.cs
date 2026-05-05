using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Application.Connections;
using ShippingOrchestrator.Domain.Connections;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Modules.Abstractions;
using ShippingOrchestrator.PublicApi.Authentication;
using Wolverine;

namespace ShippingOrchestrator.PublicApi.Endpoints;

/// <summary>
/// Tenant-scoped surface for managing per-tenant ecommerce connections from the customer SPA's
/// "Connections" page. List, start a fresh install, disconnect, and reconnect — all keyed on
/// the connection id and gated by <see cref="PublicApiPipeline.TenantPolicy"/>. Every mutating
/// path goes through the application handler, which re-checks tenant ownership before flipping
/// status. The webhook endpoint is the only path that can resolve a connection without an
/// authenticated tenant; it derives the tenant from the connection's stored
/// <see cref="EcommerceConnection.TenantId"/> rather than trusting any caller-supplied value.
/// </summary>
public static class DashboardConnectionsEndpoints
{
    public static void MapDashboardConnectionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/dashboard/connections").WithTags("Dashboard (Customer)");

        group.MapGet("", async (
            IEcommerceConnectionRepository connections,
            ConnectorRegistry registry,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var tenantId = RequireTenant(tenantContext);
            var rows = await connections.ListForTenantAsync(tenantId, ct).ConfigureAwait(false);
            var available = registry.Of(ConnectorKind.Ecommerce)
                .Select(r => new ConnectorCatalogEntry(r.ConnectorCode, r.DisplayName))
                .ToArray();
            var views = rows.Select(EcommerceConnectionView.From).ToArray();
            return Results.Ok(new ConnectionsListResponse(views, available));
        }).RequireAuthorization(PublicApiPipeline.TenantPolicy).WithName("ListEcommerceConnections");

        // Tenant-facing install checklist. The SPA renders these bullets + form fields in the
        // Connect modal before kicking off the OAuth start. Connector-owned content lives in
        // each connector module's IInstallGuideProvider — PublicApi stays platform-agnostic.
        group.MapGet("/{platform}/install-guide", (
            string platform,
            ConnectorRegistry registry,
            IServiceProvider services,
            ITenantContext tenantContext,
            HttpRequest http) =>
        {
            var tenantId = RequireTenant(tenantContext);
            var platformCode = platform.ToLowerInvariant();
            var guide = registry.ResolveInstallGuide(platformCode, services);
            if (guide is null) return Results.NotFound(new { error = $"No install guide registered for platform '{platformCode}'." });

            var mode = registry.TryGet(platformCode, out var registration) && registration is not null
                ? registration.Mode
                : ConnectorMode.Real;
            var context = new InstallGuideContext(tenantId.Value, mode, BuildCallbackBaseUrl(http));
            return Results.Ok(guide.GetGuide(context));
        }).RequireAuthorization(PublicApiPipeline.TenantPolicy).WithName("GetEcommerceInstallGuide");

        // Dashboard-driven install kickoff. The SPA passes the merchant's store URL (Shopify
        // shop domain or WooCommerce store URL); we generate a tamper-resistant install state
        // and ask the connector to build the platform-specific authorize URL with that state
        // baked in. The browser is then redirected to the URL — Shopify renders the install
        // grant page, WooCommerce shows the WC Authentication Endpoint approve screen.
        group.MapPost("/{platform}/start", async (
            string platform,
            StartConnectionInstallHttpRequest request,
            InstallStateProtector protector,
            IMessageBus bus,
            ITenantContext tenantContext,
            HttpRequest http,
            CancellationToken ct) =>
        {
            var tenantId = RequireTenant(tenantContext);
            if (string.IsNullOrWhiteSpace(request.ExternalAccountId))
                return Results.BadRequest(new { error = "externalAccountId (store URL or shop domain) is required." });

            var platformCode = platform.ToLowerInvariant();
            var state = protector.Protect(new InstallStatePayload(tenantId.Value, platformCode, request.ExternalAccountId));
            // Default to the dashboard-callback path (not the ops onboarding callback) so the
            // signed dashboard state we just minted lands at a handler that knows how to
            // decode it. Caller can override for tests / non-default deployments.
            var redirectUri = request.RedirectUri
                ?? $"{BuildCallbackBaseUrl(http)}/v1/connections/dashboard-callback/{platformCode}";

            var result = await bus.InvokeAsync<StartEcommerceOAuthResult>(
                new StartEcommerceOAuthCommand(tenantId, platformCode, request.ExternalAccountId, redirectUri, state),
                ct).ConfigureAwait(false);
            return Results.Ok(new StartConnectionInstallHttpResponse(result.AuthorizationUrl));
        }).RequireAuthorization(PublicApiPipeline.TenantPolicy).WithName("StartDashboardConnectionInstall");

        group.MapPost("/{connectionId:guid}/disconnect", async (
            Guid connectionId,
            DisconnectConnectionHttpRequest? request,
            IMessageBus bus,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var tenantId = RequireTenant(tenantContext);
            try
            {
                await bus.InvokeAsync<DisconnectEcommerceConnectionResult>(
                    new DisconnectEcommerceConnectionCommand(connectionId, tenantId, request?.Reason ?? "tenant requested"),
                    ct).ConfigureAwait(false);
                // Hard-delete: the row is gone. There's no "Disconnected" status to return —
                // a follow-up GET on /v1/dashboard/connections will simply not list this id.
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
        }).RequireAuthorization(PublicApiPipeline.TenantPolicy).WithName("DisconnectEcommerceConnection");
    }

    private static TenantId RequireTenant(ITenantContext context) =>
        context.Current ?? throw new InvalidOperationException("Tenant context is not set on this request.");

    /// <summary>
    /// Builds <c>{scheme}://{host}</c> from the incoming request, honouring forwarded headers
    /// when running behind a reverse proxy. Same code path locally (http://localhost:5101)
    /// and in production (https://api.example.com) — no env-specific branching here.
    /// </summary>
    private static string BuildCallbackBaseUrl(HttpRequest http) =>
        $"{http.Scheme}://{http.Host.Value}";
}

public sealed record StartConnectionInstallHttpRequest(string ExternalAccountId, string? RedirectUri);
public sealed record StartConnectionInstallHttpResponse(string AuthorizationUrl);
public sealed record DisconnectConnectionHttpRequest(string? Reason);
public sealed record ConnectorCatalogEntry(string ConnectorCode, string DisplayName);
public sealed record ConnectionsListResponse(
    IReadOnlyList<EcommerceConnectionView> Connections,
    IReadOnlyList<ConnectorCatalogEntry> AvailablePlatforms);

public sealed record EcommerceConnectionView(
    Guid ConnectionId,
    string PlatformCode,
    string ExternalAccountId,
    string Status,
    DateTimeOffset InstalledAt,
    DateTimeOffset? VerifiedAt,
    DateTimeOffset? LastSyncAt)
{
    internal static EcommerceConnectionView From(EcommerceConnection c) => new(
        c.Id, c.PlatformCode, c.ExternalAccountId, c.Status.ToString(),
        c.InstalledAt, c.VerifiedAt, c.LastSyncAt);
}
