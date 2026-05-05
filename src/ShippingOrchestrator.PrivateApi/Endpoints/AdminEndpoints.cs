using Microsoft.Extensions.Options;
using ShippingOrchestrator.Application.Connections;
using ShippingOrchestrator.Application.Tenancy;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.ReadModels.Abstractions.Operations;
using Wolverine;

namespace ShippingOrchestrator.PrivateApi.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var tenants = app.MapGroup("/admin/tenants").WithTags("Admin: Tenants");

        // Tenant-only ops handoff. The new tenant lands Active so the customer can log in
        // immediately; every connector install (Shopify, WooCommerce, ...) is then driven by
        // the tenant from /connections in the customer SPA. The response includes the
        // dashboard URL so ops can copy/paste it to the customer.
        tenants.MapPost("/", async (
            CreateTenantHttpRequest request,
            IMessageBus bus,
            IOptions<HostingOptions> hosting,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<CreateTenantResult>(
                new CreateTenantCommand(
                    request.DisplayName,
                    request.ContactEmail,
                    request.CarrierMode,
                    request.ToSAcceptance,
                    ActivateImmediately: request.ActivateImmediately ?? true),
                ct).ConfigureAwait(false);
            var dashboardUrl = TenantDashboardUrl.Build(hosting.Value.CustomerDashboardBaseUrl, result.TenantId.Value);
            return Results.Created(
                $"/admin/tenants/{result.TenantId}",
                new CreateTenantResponse(result.TenantId.Value, result.Status.ToString(), dashboardUrl));
        }).RequireAuthorization("Staff");

        tenants.MapPost("/{tenantId:guid}/activate", async (
            Guid tenantId, IMessageBus bus, CancellationToken ct) =>
        {
            await bus.InvokeAsync(new ActivateTenantCommand(new TenantId(tenantId)), ct).ConfigureAwait(false);
            return Results.NoContent();
        }).RequireAuthorization("Staff");

        tenants.MapPost("/{tenantId:guid}/suspend", async (
            Guid tenantId,
            SuspendTenantHttpRequest request,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            await bus.InvokeAsync(new SuspendTenantCommand(new TenantId(tenantId), request.Reason), ct).ConfigureAwait(false);
            return Results.NoContent();
        }).RequireAuthorization("Staff");

        tenants.MapPost("/{tenantId:guid}/connections/{connectionId:guid}/re-verify", async (
            Guid tenantId,
            Guid connectionId,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<ReverifyConnectionResult>(
                new ReverifyConnectionCommand(connectionId), ct).ConfigureAwait(false);
            return Results.Ok(new ReverifyConnectionResponse(result.Status, result.RejectReason));
        }).RequireAuthorization("Staff");

        tenants.MapPost("/{tenantId:guid}/carrier-assignments", async (
            Guid tenantId,
            CreateCarrierAssignmentHttpRequest request,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<CreateCarrierAssignmentResult>(
                new CreateCarrierAssignmentCommand(
                    new TenantId(tenantId),
                    request.CarrierCode,
                    request.Priority,
                    request.OriginCountries,
                    request.DestinationCountries),
                ct)
                .ConfigureAwait(false);
            return Results.Created(
                $"/admin/carrier-assignments/{result.AssignmentId}",
                new CreateCarrierAssignmentResponse(result.AssignmentId));
        }).RequireAuthorization("Staff");

        var ops = app.MapGroup("/ops").WithTags("Operations Read");

        ops.MapGet("/queues", async (
            string? status,
            int? take,
            int? skip,
            IOperationsReadQueries queries,
            CancellationToken ct) =>
        {
            var rows = await queries.ListBatchesAsync(status, take ?? 50, skip ?? 0, ct).ConfigureAwait(false);
            return Results.Ok(rows);
        }).RequireAuthorization("Staff");

        ops.MapGet("/exceptions", async (
            int? take,
            int? skip,
            IOperationsReadQueries queries,
            CancellationToken ct) =>
        {
            var rows = await queries.ListExceptionsAsync(take ?? 50, skip ?? 0, ct).ConfigureAwait(false);
            return Results.Ok(rows);
        }).RequireAuthorization("Staff");

        ops.MapGet("/kpis/carrier-success-rate", async (
            DateOnly? from,
            DateOnly? to,
            IOperationsReadQueries queries,
            CancellationToken ct) =>
        {
            var fromDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
            var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var rows = await queries.CarrierSuccessRatesAsync(fromDate, toDate, ct).ConfigureAwait(false);
            return Results.Ok(rows);
        }).RequireAuthorization("Staff");
    }
}

public sealed record CreateTenantHttpRequest(
    string DisplayName,
    string? ContactEmail,
    TenantCarrierMode? CarrierMode = null,
    ToSAcceptance? ToSAcceptance = null,
    bool? ActivateImmediately = null);

public sealed record CreateCarrierAssignmentHttpRequest(
    string CarrierCode,
    int Priority,
    IReadOnlyList<string> OriginCountries,
    IReadOnlyList<string> DestinationCountries);

public sealed record CreateTenantResponse(Guid TenantId, string Status, string DashboardUrl);

public sealed record CreateCarrierAssignmentResponse(Guid AssignmentId);

public sealed record SuspendTenantHttpRequest(string Reason);

public sealed record ReverifyConnectionResponse(string Status, string? RejectReason);
