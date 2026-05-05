using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.ReadModels.Abstractions.Operations;

public sealed record OpsOnboardingProcessRow(
    Guid ProcessId,
    string FlowCode,
    string Status,
    TenantId? TenantId,
    string? StartedByStaffUserId,
    string? ContactEmail,
    string? CurrentStepCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);

public interface IOpsOnboardingQueries
{
    Task<IReadOnlyList<OpsOnboardingProcessRow>> ListAsync(int take, int skip, CancellationToken ct);
    Task<OpsOnboardingProcessRow?> GetAsync(Guid processId, CancellationToken ct);
}
