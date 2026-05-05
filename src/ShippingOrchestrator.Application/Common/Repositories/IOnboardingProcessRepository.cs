using ShippingOrchestrator.Domain.Onboarding;

namespace ShippingOrchestrator.Application.Common.Repositories;

public interface IOnboardingProcessRepository
{
    Task<OnboardingProcess?> FindAsync(OnboardingProcessId id, CancellationToken cancellationToken);

    /// <summary>
    /// Look up a process by the external correlation token recorded on one of its steps
    /// (OAuth state, email-verify token, staff approval id). The same token is used end-to-end
    /// so a single index covers every external-resume path.
    /// </summary>
    Task<OnboardingProcess?> FindByExternalCorrelationIdAsync(string correlationId, CancellationToken cancellationToken);

    Task AddAsync(OnboardingProcess process, CancellationToken cancellationToken);

    Task<IReadOnlyList<OnboardingProcess>> ListAsync(int take, int skip, CancellationToken cancellationToken);
}
