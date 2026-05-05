using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Application.Ingestion;

/// <summary>
/// Marks an open ingestion failure as dismissed. Used by both the tenant ("I'll handle this
/// manually") and ops ("known noise, hide it") flows — the caller passes <c>DismissedBy</c>
/// for audit. <see cref="TenantId"/> scopes the lookup so a tenant cannot dismiss another
/// tenant's failure even if they guess the failure id.
/// </summary>
public sealed record DismissIngestionFailureCommand(
    Guid FailureId,
    TenantId TenantId,
    string DismissedBy);

public sealed record DismissIngestionFailureResult(bool Dismissed);

public static class DismissIngestionFailureHandler
{
    public static async Task<DismissIngestionFailureResult> Handle(
        DismissIngestionFailureCommand command,
        IIngestionFailureRepository repository,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.DismissedBy);

        var failure = await repository.FindAsync(command.FailureId, cancellationToken).ConfigureAwait(false);
        if (failure is null || failure.TenantId != command.TenantId)
            return new DismissIngestionFailureResult(false);

        failure.Dismiss(command.DismissedBy, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new DismissIngestionFailureResult(true);
    }
}
