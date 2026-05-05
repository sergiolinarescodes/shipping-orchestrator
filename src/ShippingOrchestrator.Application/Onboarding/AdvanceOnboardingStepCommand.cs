using System.Text.Json;
using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Onboarding;
using Wolverine;

namespace ShippingOrchestrator.Application.Onboarding;

public sealed record AdvanceOnboardingStepCommand(
    OnboardingProcessId ProcessId,
    string StepCode,
    JsonElement? Payload);

public sealed record AdvanceOnboardingStepResult(
    string StepCode,
    OnboardingStepStatus Status,
    string? FailureReason,
    JsonDocument? ResultPayload);

public static class AdvanceOnboardingStepHandler
{
    public static async Task<AdvanceOnboardingStepResult> Handle(
        AdvanceOnboardingStepCommand command,
        IOnboardingFlowRegistry flows,
        IOnboardingProcessRepository processes,
        IOnboardingStepDispatcher dispatcher,
        IUnitOfWork unitOfWork,
        IClock clock,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var process = await processes.FindAsync(command.ProcessId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Onboarding process {command.ProcessId} not found.");

        var flow = flows.Resolve(process.FlowCode);
        process.HydrateBlueprint(flows.BlueprintFor(process.FlowCode));

        var descriptor = flow.Steps.FirstOrDefault(s => string.Equals(s.Code, command.StepCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Step '{command.StepCode}' is not part of flow '{process.FlowCode}'.");

        var dispatch = await dispatcher
            .DispatchAsync(process, descriptor, command.Payload, cancellationToken)
            .ConfigureAwait(false);

        var now = clock.UtcNow;
        switch (dispatch.Outcome)
        {
            case OnboardingStepInvocationOutcome.Completed:
                process.CompleteStep(
                    descriptor.Code,
                    dispatch.CollectedPayload,
                    dispatch.ResultPayload,
                    dispatch.BoundTenantId,
                    now);
                break;
            case OnboardingStepInvocationOutcome.Awaiting:
                process.MarkStepAwaiting(
                    descriptor.Code,
                    dispatch.AwaitCorrelationId
                        ?? throw new InvalidOperationException("Awaiting outcome must include a correlation id."),
                    dispatch.AwaitExpiresAt,
                    now,
                    collectedPayload: dispatch.CollectedPayload,
                    resultPayload: dispatch.ResultPayload);
                if (descriptor.AwaitTimeout is { } timeout)
                {
                    await bus.ScheduleAsync(
                        new OnboardingStepTimeoutCommand(process.Id, descriptor.Code, dispatch.AwaitCorrelationId),
                        timeout).ConfigureAwait(false);
                }
                break;
            case OnboardingStepInvocationOutcome.Failed:
                process.FailStep(descriptor.Code, dispatch.FailureReason ?? "step failed", dispatch.CollectedPayload, now);
                break;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // After a manual step completes, drive any immediately-following automatic steps so a
        // future flow can chain auto-progression without the UI clicking each one. Today's
        // single-step flow has no automatic successors so this is a fast no-op.
        if (dispatch.Outcome == OnboardingStepInvocationOutcome.Completed)
            await ProgressAutomaticStepsAsync(process, flow, bus).ConfigureAwait(false);

        var step = process.Steps.First(s => s.Code == descriptor.Code);
        return new AdvanceOnboardingStepResult(
            descriptor.Code,
            step.Status,
            step.FailureReason,
            step.ResultPayload);
    }

    private static async Task ProgressAutomaticStepsAsync(
        OnboardingProcess process,
        IOnboardingFlow flow,
        IMessageBus bus)
    {
        var nextAuto = flow.Steps
            .OrderBy(s => s.Sequence)
            .FirstOrDefault(s =>
                s.Kind == OnboardingStepKind.Automatic
                && process.Steps.Single(p => p.Code == s.Code).Status == OnboardingStepStatus.Pending
                && process.Steps
                    .Where(p => p.Sequence < s.Sequence)
                    .All(p => p.Status is OnboardingStepStatus.Completed or OnboardingStepStatus.Skipped));
        if (nextAuto is null) return;
        await bus.PublishAsync(new AdvanceOnboardingStepCommand(process.Id, nextAuto.Code, Payload: null))
            .ConfigureAwait(false);
    }
}
