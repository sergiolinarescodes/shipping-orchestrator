using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Onboarding;

namespace ShippingOrchestrator.Application.Onboarding;

/// <summary>
/// Wolverine-scheduled message that fires once an awaiting step's timeout elapses. The
/// handler re-reads the process; if the step is still <c>Awaiting</c> with the same
/// correlation id, the process moves to <c>TimedOut</c>. Otherwise the timeout is a no-op
/// (the callback already arrived, or the awaitee was rearmed with a fresh correlation id).
/// </summary>
public sealed record OnboardingStepTimeoutCommand(
    OnboardingProcessId ProcessId,
    string StepCode,
    string CorrelationId);

public static class OnboardingStepTimeoutHandler
{
    public static async Task Handle(
        OnboardingStepTimeoutCommand command,
        IOnboardingProcessRepository processes,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var process = await processes.FindAsync(command.ProcessId, cancellationToken).ConfigureAwait(false);
        if (process is null) return;
        if (process.Status != OnboardingProcessStatus.InProgress) return;

        var step = process.Steps.FirstOrDefault(s => s.Code == command.StepCode);
        if (step is null) return;
        if (step.Status != OnboardingStepStatus.Awaiting) return;
        if (step.ExternalCorrelationId != command.CorrelationId) return; // rearmed since

        process.TimeOut(command.StepCode, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
