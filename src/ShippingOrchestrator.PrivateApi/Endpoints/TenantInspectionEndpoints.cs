using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Modules.Abstractions;
using ShippingOrchestrator.ReadModels.Abstractions.Operations;

namespace ShippingOrchestrator.PrivateApi.Endpoints;

/// <summary>
/// Read-only inspection endpoints used by the internal dashboard's tenant detail view: full
/// tenant + connections + recent batches in one request, and a separate connections endpoint
/// for the integrations panel. Joins live on the read schema where possible (batches) and on
/// the orchestrator schema for connection metadata (the read schema doesn't carry a
/// connector inventory yet — the connector registry is the source of truth at runtime).
/// </summary>
public static class TenantInspectionEndpoints
{
    public static void MapTenantInspectionEndpoints(this IEndpointRouteBuilder app)
    {
        var tenants = app.MapGroup("/admin/tenants").WithTags("Admin: Tenant Inspection");

        tenants.MapGet("/", async (
            int? take,
            int? skip,
            IOperationsReadQueries opsQueries,
            CancellationToken ct) =>
        {
            var rows = await opsQueries.ListTenantsAsync(take ?? 50, skip ?? 0, ct).ConfigureAwait(false);
            return Results.Ok(rows);
        }).RequireAuthorization("Staff");

        tenants.MapGet("/{tenantId:guid}", async (
            Guid tenantId,
            ITenantRepository tenantRepo,
            IEcommerceConnectionRepository ecommerceRepo,
            ICarrierAssignmentRepository carrierRepo,
            IOperationsReadQueries opsQueries,
            ConnectorRegistry registry,
            CancellationToken ct) =>
        {
            var typed = new TenantId(tenantId);
            var tenant = await tenantRepo.FindAsync(typed, ct).ConfigureAwait(false);
            if (tenant is null) return Results.NotFound();

            var connections = await ecommerceRepo.ListForTenantAsync(typed, ct).ConfigureAwait(false);
            var assignments = await carrierRepo.ListForTenantAsync(typed, ct).ConfigureAwait(false);
            var batches = await opsQueries.ListBatchesAsync(statusFilter: null, take: 10, skip: 0, ct).ConfigureAwait(false);

            var ecommerceViews = connections
                .Select(c => new TenantEcommerceConnectionView(
                    c.Id,
                    c.PlatformCode,
                    c.ExternalAccountId,
                    c.InstalledAt,
                    c.LastSyncAt,
                    ResolveMode(registry, c.PlatformCode),
                    c.Status.ToString(),
                    c.VerifiedAt,
                    c.RejectedAt,
                    c.RejectionReason))
                .ToArray();

            var carrierViews = assignments
                .Select(a => new TenantCarrierAssignmentView(
                    a.Id,
                    a.CarrierCode,
                    a.Priority,
                    a.IsActive,
                    a.OriginCountries,
                    a.DestinationCountries,
                    ResolveMode(registry, a.CarrierCode)))
                .ToArray();

            var tenantBatches = batches
                .Where(b => b.TenantId == typed)
                .ToArray();

            return Results.Ok(new TenantDetailView(
                tenant.Id.Value,
                tenant.DisplayName,
                tenant.Status.ToString(),
                tenant.ContactEmail,
                tenant.CreatedAt,
                tenant.UpdatedAt,
                tenant.CarrierMode?.ToString(),
                tenant.ToSAcceptance,
                ecommerceViews,
                carrierViews,
                tenantBatches));
        }).RequireAuthorization("Staff");

        tenants.MapGet("/{tenantId:guid}/connections", async (
            Guid tenantId,
            IEcommerceConnectionRepository ecommerceRepo,
            ICarrierAssignmentRepository carrierRepo,
            ConnectorRegistry registry,
            CancellationToken ct) =>
        {
            var typed = new TenantId(tenantId);
            var connections = await ecommerceRepo.ListForTenantAsync(typed, ct).ConfigureAwait(false);
            var assignments = await carrierRepo.ListForTenantAsync(typed, ct).ConfigureAwait(false);
            return Results.Ok(new TenantConnectionsView(
                connections.Select(c => new TenantEcommerceConnectionView(
                    c.Id, c.PlatformCode, c.ExternalAccountId, c.InstalledAt, c.LastSyncAt,
                    ResolveMode(registry, c.PlatformCode),
                    c.Status.ToString(), c.VerifiedAt, c.RejectedAt, c.RejectionReason)).ToArray(),
                assignments.Select(a => new TenantCarrierAssignmentView(
                    a.Id, a.CarrierCode, a.Priority, a.IsActive, a.OriginCountries, a.DestinationCountries,
                    ResolveMode(registry, a.CarrierCode))).ToArray()));
        }).RequireAuthorization("Staff");
    }

    private static string? ResolveMode(ConnectorRegistry registry, string code) =>
        registry.TryGet(code, out var registration) && registration is not null
            ? (registration.Metadata?.TryGetValue("mode", out var m) == true ? m : null)
            : null;
}

public sealed record TenantDetailView(
    Guid TenantId,
    string DisplayName,
    string Status,
    string? ContactEmail,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? CarrierMode,
    ToSAcceptance? ToSAcceptance,
    IReadOnlyList<TenantEcommerceConnectionView> EcommerceConnections,
    IReadOnlyList<TenantCarrierAssignmentView> CarrierAssignments,
    IReadOnlyList<OpsBatchRow> RecentBatches);

public sealed record TenantConnectionsView(
    IReadOnlyList<TenantEcommerceConnectionView> EcommerceConnections,
    IReadOnlyList<TenantCarrierAssignmentView> CarrierAssignments);

public sealed record TenantEcommerceConnectionView(
    Guid ConnectionId,
    string PlatformCode,
    string ExternalAccountId,
    DateTimeOffset InstalledAt,
    DateTimeOffset? LastSyncAt,
    string? Mode,
    string Status,
    DateTimeOffset? VerifiedAt,
    DateTimeOffset? RejectedAt,
    string? RejectionReason);

public sealed record TenantCarrierAssignmentView(
    Guid AssignmentId,
    string CarrierCode,
    int Priority,
    bool IsActive,
    IReadOnlyList<string> OriginCountries,
    IReadOnlyList<string> DestinationCountries,
    string? Mode);
