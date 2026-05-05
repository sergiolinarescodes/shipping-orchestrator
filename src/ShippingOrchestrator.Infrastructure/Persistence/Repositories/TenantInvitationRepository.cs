using Microsoft.EntityFrameworkCore;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Identity;

namespace ShippingOrchestrator.Infrastructure.Persistence.Repositories;

internal sealed class TenantInvitationRepository(OrchestratorDbContext db) : ITenantInvitationRepository
{
    public async Task AddAsync(TenantInvitation invitation, CancellationToken cancellationToken) =>
        await db.TenantInvitations.AddAsync(invitation, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<TenantInvitation>> ListPendingByEmailAsync(
        string email, CancellationToken cancellationToken)
    {
        var normalized = Domain.Identity.Account.NormalizeEmail(email);
        return await db.TenantInvitations
            .Where(i => i.Email == normalized && i.ConsumedAt == null && i.RevokedAt == null)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
