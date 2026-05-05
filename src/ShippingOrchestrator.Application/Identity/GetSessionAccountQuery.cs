using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Identity;

namespace ShippingOrchestrator.Application.Identity;

public sealed record GetSessionAccountQuery(Guid SessionId);

public static class GetSessionAccountHandler
{
    public static async Task<SessionAccountView?> Handle(
        GetSessionAccountQuery query,
        IAuthSessionRepository sessions,
        IAccountRepository accounts,
        ITenantMembershipRepository memberships,
        ITenantRepository tenants,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var session = await sessions.FindByIdAsync(query.SessionId, cancellationToken).ConfigureAwait(false);
        if (session is null || !session.IsActive(clock.UtcNow)) return null;

        var account = await accounts.FindByIdAsync(session.AccountId, cancellationToken).ConfigureAwait(false);
        if (account is null) return null;

        var rows = await memberships.ListForAccountAsync(account.Id, cancellationToken).ConfigureAwait(false);
        var views = new List<SessionTenantMembershipView>(rows.Count);
        foreach (var m in rows)
        {
            var tenant = await tenants.FindAsync(m.TenantId, cancellationToken).ConfigureAwait(false);
            if (tenant is null) continue;
            views.Add(new SessionTenantMembershipView(
                tenant.Id, tenant.DisplayName, tenant.Status.ToString(), m.Role));
        }

        return new SessionAccountView(
            account.Id, account.Email, account.DisplayName,
            session.CurrentTenantId, views);
    }
}
