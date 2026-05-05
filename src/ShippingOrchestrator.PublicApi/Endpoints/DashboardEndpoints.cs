using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.ReadModels.Abstractions.Customer;

namespace ShippingOrchestrator.PublicApi.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/dashboard").WithTags("Dashboard (Customer)");

        group.MapGet("/shipments", async (
            int? take,
            int? skip,
            ICustomerReadQueries queries,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var tenantId = tenantContext.Current
                ?? throw new InvalidOperationException("Tenant context is not set on this request.");
            var rows = await queries.ListShipmentsAsync(tenantId, take ?? 50, skip ?? 0, ct).ConfigureAwait(false);
            return Results.Ok(rows);
        }).RequireAuthorization("Tenant").WithName("ListCustomerShipments");

        group.MapGet("/batches", async (
            int? take,
            int? skip,
            string? status,
            ICustomerReadQueries queries,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var tenantId = tenantContext.Current
                ?? throw new InvalidOperationException("Tenant context is not set on this request.");
            var rows = await queries
                .ListBatchesAsync(tenantId, take ?? 50, skip ?? 0, status, ct)
                .ConfigureAwait(false);
            return Results.Ok(rows);
        }).RequireAuthorization("Tenant").WithName("ListCustomerBatches");

        group.MapGet("/tenant", async (
            ITenantRepository tenants,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var tenantId = tenantContext.Current
                ?? throw new InvalidOperationException("Tenant context is not set on this request.");
            var tenant = await tenants.FindAsync(tenantId, ct).ConfigureAwait(false);
            if (tenant is null) return Results.NotFound();
            return Results.Ok(new CurrentTenantView(
                tenant.Id.Value,
                tenant.DisplayName,
                tenant.Status.ToString(),
                tenant.ContactEmail));
        }).RequireAuthorization("Tenant").WithName("GetCurrentTenant");
    }
}

public sealed record CurrentTenantView(Guid TenantId, string DisplayName, string Status, string? ContactEmail);

public sealed record DevTenantSummary(Guid TenantId, string DisplayName, string Status, DateTimeOffset CreatedAt);
