using ShippingOrchestrator.Application.Ingestion;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.ReadModels.Abstractions.Customer;
using Wolverine;

namespace ShippingOrchestrator.PublicApi.Endpoints;

/// <summary>
/// Tenant-facing surface for ingestion failures — the "Needs attention" tab on the dashboard.
/// List/get are tenant-scoped via <see cref="ITenantContext"/>; the dismiss action records
/// audit context as <c>tenant:{tenantId}</c> so ops can later distinguish self-dismissals from
/// staff-side hides.
/// </summary>
public static class NeedsAttentionEndpoints
{
    public static void MapNeedsAttentionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/dashboard/orders/needs-attention").WithTags("Dashboard (Customer)");

        group.MapGet("", async (
            string? status,
            int? take,
            int? skip,
            ICustomerReadQueries queries,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var tenantId = tenantContext.Current
                ?? throw new InvalidOperationException("Tenant context is not set on this request.");
            var rows = await queries.ListIngestionFailuresAsync(
                tenantId, status, take ?? 100, skip ?? 0, ct).ConfigureAwait(false);
            return Results.Ok(rows);
        }).RequireAuthorization("Tenant").WithName("ListIngestionFailures");

        group.MapGet("/count", async (
            ICustomerReadQueries queries,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var tenantId = tenantContext.Current
                ?? throw new InvalidOperationException("Tenant context is not set on this request.");
            var count = await queries.CountOpenIngestionFailuresAsync(tenantId, ct).ConfigureAwait(false);
            return Results.Ok(new { open = count });
        }).RequireAuthorization("Tenant").WithName("CountOpenIngestionFailures");

        group.MapGet("/{failureId:guid}", async (
            Guid failureId,
            ICustomerReadQueries queries,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var tenantId = tenantContext.Current
                ?? throw new InvalidOperationException("Tenant context is not set on this request.");
            var row = await queries.GetIngestionFailureAsync(tenantId, failureId, ct).ConfigureAwait(false);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).RequireAuthorization("Tenant").WithName("GetIngestionFailure");

        group.MapPost("/{failureId:guid}/dismiss", async (
            Guid failureId,
            IMessageBus bus,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var tenantId = tenantContext.Current
                ?? throw new InvalidOperationException("Tenant context is not set on this request.");
            var result = await bus.InvokeAsync<DismissIngestionFailureResult>(
                new DismissIngestionFailureCommand(failureId, tenantId, $"tenant:{tenantId.Value}"),
                ct).ConfigureAwait(false);
            return result.Dismissed ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("Tenant").WithName("DismissIngestionFailure");

        group.MapPost("/{failureId:guid}/recheck", async (
            Guid failureId,
            IMessageBus bus,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var tenantId = tenantContext.Current
                ?? throw new InvalidOperationException("Tenant context is not set on this request.");
            var result = await bus.InvokeAsync<RecheckIngestionFailureResult>(
                new RecheckIngestionFailureCommand(failureId, tenantId), ct).ConfigureAwait(false);
            return MapRecheckResult(result);
        }).RequireAuthorization("Tenant").WithName("RecheckIngestionFailure");
    }

    /// <summary>
    /// Translates the typed <see cref="RecheckOutcome"/> into HTTP semantics. Outcomes that
    /// represent a state change (Resolved, StillFailing) return 200 with a body. Outcomes
    /// caused by missing/invalid input (NotFound, NotRecheckable) return 4xx. Connector or
    /// platform problems (NoConnection, NotSupported, FetchFailed) return 422 since the
    /// request was well-formed but couldn't be carried out.
    /// </summary>
    private static IResult MapRecheckResult(RecheckIngestionFailureResult result) => result.Outcome switch
    {
        RecheckOutcome.Resolved => Results.Ok(new { outcome = "resolved", pendingOrderId = result.PendingOrderId }),
        RecheckOutcome.StillFailing => Results.Ok(new { outcome = "still_failing", detail = result.Detail }),
        RecheckOutcome.NotFound => Results.NotFound(),
        RecheckOutcome.NotRecheckable => Results.BadRequest(new { outcome = "not_recheckable", detail = result.Detail }),
        RecheckOutcome.AmbiguousConnection => Results.UnprocessableEntity(new { outcome = "ambiguous_connection", detail = result.Detail }),
        RecheckOutcome.NoConnection => Results.UnprocessableEntity(new { outcome = "no_connection", detail = result.Detail }),
        RecheckOutcome.NotSupported => Results.UnprocessableEntity(new { outcome = "not_supported", detail = result.Detail }),
        RecheckOutcome.FetchFailed => Results.UnprocessableEntity(new { outcome = "fetch_failed", detail = result.Detail }),
        _ => Results.UnprocessableEntity(new { outcome = result.Outcome.ToString(), detail = result.Detail }),
    };
}
