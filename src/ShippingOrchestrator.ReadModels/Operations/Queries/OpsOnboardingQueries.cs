using Microsoft.EntityFrameworkCore;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.ReadModels.Abstractions.Operations;
using ShippingOrchestrator.ReadModels.Operations.Persistence;

namespace ShippingOrchestrator.ReadModels.Operations.Queries;

internal sealed class OpsOnboardingQueries(OperationsReadDbContext db) : IOpsOnboardingQueries
{
    public async Task<IReadOnlyList<OpsOnboardingProcessRow>> ListAsync(int take, int skip, CancellationToken ct)
    {
        var rows = await db.OnboardingProcesses
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip).Take(take)
            .ToArrayAsync(ct)
            .ConfigureAwait(false);
        return rows.Select(Project).ToArray();
    }

    public async Task<OpsOnboardingProcessRow?> GetAsync(Guid processId, CancellationToken ct)
    {
        var row = await db.OnboardingProcesses
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProcessId == processId, ct)
            .ConfigureAwait(false);
        return row is null ? null : Project(row);
    }

    private static OpsOnboardingProcessRow Project(OpsOnboardingProcessEntity e) => new(
        e.ProcessId,
        e.FlowCode,
        e.Status,
        e.TenantId is null ? null : new TenantId(e.TenantId.Value),
        e.StartedByStaffUserId,
        e.ContactEmail,
        e.CurrentStepCode,
        e.CreatedAt,
        e.UpdatedAt,
        e.CompletedAt);
}
