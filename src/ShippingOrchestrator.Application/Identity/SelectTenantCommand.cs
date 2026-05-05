using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Identity;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Application.Identity;

public sealed record SelectTenantCommand(Guid SessionId, TenantId TenantId);

public sealed record SelectTenantResult(bool Success, string? FailureReason);

public static class SelectTenantHandler
{
    public static async Task<SelectTenantResult> Handle(
        SelectTenantCommand command,
        IAuthSessionRepository sessions,
        ITenantMembershipRepository memberships,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var session = await sessions.FindByIdAsync(command.SessionId, cancellationToken).ConfigureAwait(false);
        if (session is null) return new SelectTenantResult(false, "no-session");

        var now = clock.UtcNow;
        if (!session.IsActive(now)) return new SelectTenantResult(false, "session-expired");

        var membership = await memberships.FindAsync(
            session.AccountId, command.TenantId, cancellationToken).ConfigureAwait(false);
        if (membership is null) return new SelectTenantResult(false, "no-membership");

        session.SelectTenant(command.TenantId, now);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new SelectTenantResult(true, null);
    }
}
