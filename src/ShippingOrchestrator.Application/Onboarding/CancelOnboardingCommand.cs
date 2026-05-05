using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Onboarding;

namespace ShippingOrchestrator.Application.Onboarding;

public sealed record CancelOnboardingCommand(OnboardingProcessId ProcessId, string? Reason);

public static class CancelOnboardingHandler
{
    public static async Task Handle(
        CancelOnboardingCommand command,
        IOnboardingProcessRepository processes,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var process = await processes.FindAsync(command.ProcessId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Onboarding process {command.ProcessId} not found.");
        process.Cancel(command.Reason, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
