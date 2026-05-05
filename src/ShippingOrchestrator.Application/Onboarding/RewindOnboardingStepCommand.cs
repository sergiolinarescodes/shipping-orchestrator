using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Onboarding;

namespace ShippingOrchestrator.Application.Onboarding;

public sealed record RewindOnboardingStepCommand(OnboardingProcessId ProcessId, string TargetStepCode);

public sealed record RewindOnboardingStepResult(bool Rewound, string? CommitBoundary);

public static class RewindOnboardingStepHandler
{
    public static async Task<RewindOnboardingStepResult> Handle(
        RewindOnboardingStepCommand command,
        IOnboardingFlowRegistry flows,
        IOnboardingProcessRepository processes,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var process = await processes.FindAsync(command.ProcessId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Onboarding process {command.ProcessId} not found.");

        process.HydrateBlueprint(flows.BlueprintFor(process.FlowCode));
        var commitBoundary = process.RewindTo(command.TargetStepCode, clock.UtcNow);
        if (commitBoundary is not null) return new RewindOnboardingStepResult(false, commitBoundary);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new RewindOnboardingStepResult(true, null);
    }
}
