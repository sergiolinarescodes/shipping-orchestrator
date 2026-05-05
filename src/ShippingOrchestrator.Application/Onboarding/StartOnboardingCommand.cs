using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Onboarding;

namespace ShippingOrchestrator.Application.Onboarding;

public sealed record StartOnboardingCommand(
    string FlowCode,
    string? StartedByStaffUserId,
    string? ContactEmail);

public sealed record StartOnboardingResult(OnboardingProcessId ProcessId);

public static class StartOnboardingHandler
{
    public static async Task<StartOnboardingResult> Handle(
        StartOnboardingCommand command,
        IOnboardingFlowRegistry flows,
        IOnboardingProcessRepository processes,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var blueprint = flows.BlueprintFor(command.FlowCode);
        var process = OnboardingProcess.Start(
            blueprint,
            command.StartedByStaffUserId,
            command.ContactEmail,
            clock.UtcNow);

        await processes.AddAsync(process, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new StartOnboardingResult(process.Id);
    }
}
