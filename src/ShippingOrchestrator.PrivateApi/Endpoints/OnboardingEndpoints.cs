using System.Text.Json;
using Microsoft.Extensions.Options;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Application.Onboarding;
using ShippingOrchestrator.Application.Onboarding.Flows;
using ShippingOrchestrator.Domain.Onboarding;
using Wolverine;

namespace ShippingOrchestrator.PrivateApi.Endpoints;

public static class OnboardingEndpoints
{
    public static void MapOnboardingEndpoints(this IEndpointRouteBuilder app)
    {
        var onboarding = app.MapGroup("/admin/onboarding").WithTags("Admin: Onboarding");

        onboarding.MapGet("/flows", (IOnboardingFlowRegistry registry) =>
            Results.Ok(registry.All.Select(OnboardingProcessViewProjector.Summary).ToArray()))
            .RequireAuthorization("Staff");

        onboarding.MapPost("/", async (
            StartOnboardingHttpRequest request,
            IMessageBus bus,
            HttpContext http,
            CancellationToken ct) =>
        {
            var staffUser = http.User.Identity?.Name;
            var result = await bus.InvokeAsync<StartOnboardingResult>(
                new StartOnboardingCommand(request.FlowCode, staffUser, request.ContactEmail), ct)
                .ConfigureAwait(false);
            return Results.Created(
                $"/admin/onboarding/{result.ProcessId}",
                new StartOnboardingResponse(result.ProcessId.Value));
        }).RequireAuthorization("Staff");

        onboarding.MapGet("/", async (
            IOnboardingProcessRepository processes,
            IOnboardingFlowRegistry flows,
            IOptions<HostingOptions> hosting,
            int? take, int? skip,
            CancellationToken ct) =>
        {
            var list = await processes.ListAsync(take ?? 50, skip ?? 0, ct).ConfigureAwait(false);
            var views = list
                .Where(p => flows.TryResolve(p.FlowCode, out _))
                .Select(p => WithDashboardUrl(
                    OnboardingProcessViewProjector.Project(p, flows.Resolve(p.FlowCode)),
                    hosting.Value))
                .ToArray();
            return Results.Ok(views);
        }).RequireAuthorization("Staff");

        onboarding.MapGet("/{processId:guid}", async (
            Guid processId,
            IOnboardingProcessRepository processes,
            IOnboardingFlowRegistry flows,
            IOptions<HostingOptions> hosting,
            CancellationToken ct) =>
        {
            var process = await processes.FindAsync(new OnboardingProcessId(processId), ct).ConfigureAwait(false);
            if (process is null) return Results.NotFound();
            if (!flows.TryResolve(process.FlowCode, out var flow) || flow is null)
                return Results.Problem(
                    title: "Flow not registered",
                    detail: $"Flow '{process.FlowCode}' is not registered in this host.",
                    statusCode: StatusCodes.Status409Conflict);
            var view = WithDashboardUrl(OnboardingProcessViewProjector.Project(process, flow), hosting.Value);
            return Results.Ok(view);
        }).RequireAuthorization("Staff");

        onboarding.MapPost("/{processId:guid}/steps/{stepCode}/advance", async (
            Guid processId,
            string stepCode,
            JsonElement? payload,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            try
            {
                var result = await bus.InvokeAsync<AdvanceOnboardingStepResult>(
                    new AdvanceOnboardingStepCommand(new OnboardingProcessId(processId), stepCode, payload), ct)
                    .ConfigureAwait(false);
                return result.Status switch
                {
                    OnboardingStepStatus.Failed => Results.UnprocessableEntity(
                        new AdvanceFailedResponse(result.StepCode, result.FailureReason ?? "step failed")),
                    _ => Results.Ok(new AdvanceOkResponse(result.StepCode, result.Status.ToString())),
                };
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        }).RequireAuthorization("Staff");

        onboarding.MapPost("/{processId:guid}/steps/{stepCode}/rewind", async (
            Guid processId,
            string stepCode,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<RewindOnboardingStepResult>(
                new RewindOnboardingStepCommand(new OnboardingProcessId(processId), stepCode), ct)
                .ConfigureAwait(false);
            return result.Rewound
                ? Results.Ok(new { rewound = true })
                : Results.Conflict(new { rewound = false, commitBoundary = result.CommitBoundary });
        }).RequireAuthorization("Staff");

        onboarding.MapPost("/{processId:guid}/cancel", async (
            Guid processId,
            CancelOnboardingHttpRequest? body,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            await bus.InvokeAsync(
                new CancelOnboardingCommand(new OnboardingProcessId(processId), body?.Reason), ct)
                .ConfigureAwait(false);
            return Results.NoContent();
        }).RequireAuthorization("Staff");

    }

    private static OnboardingProcessView WithDashboardUrl(OnboardingProcessView view, HostingOptions hosting) =>
        view.TenantId is { } tid
            ? view with { DashboardUrl = TenantDashboardUrl.Build(hosting.CustomerDashboardBaseUrl, tid) }
            : view;
}

public sealed record StartOnboardingHttpRequest(string FlowCode, string? ContactEmail);
public sealed record StartOnboardingResponse(Guid ProcessId);
public sealed record CancelOnboardingHttpRequest(string? Reason);
public sealed record AdvanceOkResponse(string StepCode, string Status);
public sealed record AdvanceFailedResponse(string StepCode, string FailureReason);
