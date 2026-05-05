using System.Security.Claims;
using ShippingOrchestrator.Application.Ingestion;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.ReadModels.Abstractions.Operations;
using Wolverine;

namespace ShippingOrchestrator.PrivateApi.Endpoints;

/// <summary>
/// Operations console surface for ingestion failures — list with filters, single-row detail,
/// staff-side dismiss with audit, and a (tenant × reason) stats pivot for trend spotting.
/// All routes require the Staff policy. Audit identity comes from the authenticated staff
/// user's NameIdentifier claim, so dismissal records survive even if the operator's role
/// changes later.
/// </summary>
public static class IngestionFailuresOpsEndpoints
{
    public static void MapIngestionFailuresOpsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/ops/ingestion-failures").WithTags("Ops: Ingestion failures");

        group.MapGet("", async (
            Guid? tenantId,
            string? connectorCode,
            string? reasonCode,
            string? status,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? take,
            int? skip,
            IOperationsReadQueries queries,
            CancellationToken ct) =>
        {
            var filter = new OpsIngestionFailureFilter(
                TenantId: tenantId,
                ConnectorCode: connectorCode,
                ReasonCode: reasonCode,
                Status: status,
                FromUtc: from,
                ToUtc: to,
                Take: take ?? 100,
                Skip: skip ?? 0);
            var rows = await queries.ListIngestionFailuresAsync(filter, ct).ConfigureAwait(false);
            return Results.Ok(rows);
        }).RequireAuthorization("Staff").WithName("OpsListIngestionFailures");

        group.MapGet("/stats", async (
            string? window,
            IOperationsReadQueries queries,
            CancellationToken ct) =>
        {
            var fromUtc = DateTimeOffset.UtcNow - ParseWindow(window ?? "24h");
            var rows = await queries.IngestionFailureStatsAsync(fromUtc, ct).ConfigureAwait(false);
            return Results.Ok(rows);
        }).RequireAuthorization("Staff").WithName("OpsIngestionFailureStats");

        group.MapGet("/{failureId:guid}", async (
            Guid failureId,
            IOperationsReadQueries queries,
            CancellationToken ct) =>
        {
            var row = await queries.GetIngestionFailureAsync(failureId, ct).ConfigureAwait(false);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).RequireAuthorization("Staff").WithName("OpsGetIngestionFailure");

        group.MapPost("/{failureId:guid}/dismiss", async (
            Guid failureId,
            IMessageBus bus,
            IOperationsReadQueries queries,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var existing = await queries.GetIngestionFailureAsync(failureId, ct).ConfigureAwait(false);
            if (existing is null) return Results.NotFound();
            var staffUser = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "staff";

            var result = await bus.InvokeAsync<DismissIngestionFailureResult>(
                new DismissIngestionFailureCommand(failureId, new TenantId(existing.TenantId), $"staff:{staffUser}"),
                ct).ConfigureAwait(false);
            return result.Dismissed ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("Staff").WithName("OpsDismissIngestionFailure");

        group.MapPost("/{failureId:guid}/recheck", async (
            Guid failureId,
            IMessageBus bus,
            IOperationsReadQueries queries,
            CancellationToken ct) =>
        {
            // Look up the row first so ops sees the same NotFound semantics regardless of
            // whether the read projection is behind. The handler re-validates tenant scope.
            var existing = await queries.GetIngestionFailureAsync(failureId, ct).ConfigureAwait(false);
            if (existing is null) return Results.NotFound();

            var result = await bus.InvokeAsync<RecheckIngestionFailureResult>(
                new RecheckIngestionFailureCommand(failureId, new TenantId(existing.TenantId)),
                ct).ConfigureAwait(false);

            return result.Outcome switch
            {
                RecheckOutcome.Resolved => Results.Ok(new { outcome = "resolved", pendingOrderId = result.PendingOrderId }),
                RecheckOutcome.StillFailing => Results.Ok(new { outcome = "still_failing", detail = result.Detail }),
                RecheckOutcome.NotFound => Results.NotFound(),
                RecheckOutcome.NotRecheckable => Results.BadRequest(new { outcome = "not_recheckable", detail = result.Detail }),
                _ => Results.UnprocessableEntity(new { outcome = result.Outcome.ToString().ToLowerInvariant(), detail = result.Detail }),
            };
        }).RequireAuthorization("Staff").WithName("OpsRecheckIngestionFailure");
    }

    private static TimeSpan ParseWindow(string window)
    {
        // Accepts "30m", "24h", "7d". Falls back to 24h on garbage rather than failing the
        // request — ops dashboards shouldn't 400 on a typo in a query string.
        if (string.IsNullOrWhiteSpace(window) || window.Length < 2) return TimeSpan.FromHours(24);
        var unit = window[^1];
        if (!int.TryParse(window.AsSpan(0, window.Length - 1), out var value) || value <= 0)
            return TimeSpan.FromHours(24);
        return unit switch
        {
            'm' => TimeSpan.FromMinutes(value),
            'h' => TimeSpan.FromHours(value),
            'd' => TimeSpan.FromDays(value),
            _ => TimeSpan.FromHours(24),
        };
    }
}
