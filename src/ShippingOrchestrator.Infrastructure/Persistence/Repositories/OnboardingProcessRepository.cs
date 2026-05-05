using Microsoft.EntityFrameworkCore;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Onboarding;

namespace ShippingOrchestrator.Infrastructure.Persistence.Repositories;

internal sealed class OnboardingProcessRepository(OrchestratorDbContext db) : IOnboardingProcessRepository
{
    public Task<OnboardingProcess?> FindAsync(OnboardingProcessId id, CancellationToken cancellationToken) =>
        db.OnboardingProcesses
            .Include(p => p.Steps)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<OnboardingProcess?> FindByExternalCorrelationIdAsync(
        string correlationId, CancellationToken cancellationToken)
    {
        // The correlation id lives on a step record; find the parent via the join.
        var step = await db.Set<OnboardingStepRecord>()
            .AsNoTracking()
            .Where(s => s.ExternalCorrelationId == correlationId)
            .Select(s => new { s.ProcessId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (step is null) return null;
        return await FindAsync(step.ProcessId, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(OnboardingProcess process, CancellationToken cancellationToken) =>
        await db.OnboardingProcesses.AddAsync(process, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<OnboardingProcess>> ListAsync(int take, int skip, CancellationToken cancellationToken) =>
        await db.OnboardingProcesses
            .Include(p => p.Steps)
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip).Take(take)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
}
