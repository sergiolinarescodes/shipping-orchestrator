using Microsoft.Extensions.Options;
using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Identity;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Application.Identity;

public sealed record ConsumeMagicLinkCommand(string TokenPlaintext);

public sealed record ConsumeMagicLinkResult(
    bool Success,
    AccountId? AccountId,
    string? RawSessionToken,
    DateTimeOffset? SessionExpiresAt,
    string? FailureReason);

/// <summary>
/// Verifies a magic-link plaintext, single-use-consumes the token row, finds-or-creates the
/// account, auto-grants memberships from matching tenant ContactEmail and any pending
/// <see cref="TenantInvitation"/>s, and issues a fresh <see cref="AuthSession"/>. Returns the
/// raw session token; only its hash is persisted.
/// </summary>
public static class ConsumeMagicLinkHandler
{
    public static async Task<ConsumeMagicLinkResult> Handle(
        ConsumeMagicLinkCommand command,
        IMagicLinkTokenRepository tokens,
        IAccountRepository accounts,
        ITenantRepository tenants,
        ITenantMembershipRepository memberships,
        ITenantInvitationRepository invitations,
        IAuthSessionRepository sessions,
        IUnitOfWork unitOfWork,
        IOptions<AuthOptions> options,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.TokenPlaintext))
            return new ConsumeMagicLinkResult(false, null, null, null, "missing-token");

        var hash = TokenGenerator.Hash(command.TokenPlaintext);
        var token = await tokens.FindByHashAsync(hash, cancellationToken).ConfigureAwait(false);
        if (token is null)
            return new ConsumeMagicLinkResult(false, null, null, null, "unknown-token");

        var now = clock.UtcNow;
        if (!token.IsConsumable(now))
            return new ConsumeMagicLinkResult(false, null, null, null,
                token.ConsumedAt is not null ? "already-consumed" : "expired");

        token.Consume(now);

        var account = await accounts.FindByEmailAsync(token.Email, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            account = Account.Create(token.Email, now);
            await accounts.AddAsync(account, cancellationToken).ConfigureAwait(false);
        }

        await SeedMembershipsFromContactEmailAsync(
            account, tenants, memberships, now, cancellationToken).ConfigureAwait(false);

        await ConsumePendingInvitationsAsync(
            account, invitations, memberships, now, cancellationToken).ConfigureAwait(false);

        account.RecordSignIn(now);

        var rawSession = TokenGenerator.NewRawSecret();
        var sessionHash = TokenGenerator.Hash(rawSession);
        var sessionTtl = TimeSpan.FromSeconds(options.Value.SessionTtlSeconds);
        var session = AuthSession.Issue(account.Id, sessionHash, now, sessionTtl);
        await sessions.AddAsync(session, cancellationToken).ConfigureAwait(false);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ConsumeMagicLinkResult(
            true,
            account.Id,
            rawSession,
            session.ExpiresAt,
            null);
    }

    private static async Task SeedMembershipsFromContactEmailAsync(
        Account account,
        ITenantRepository tenants,
        ITenantMembershipRepository memberships,
        DateTimeOffset now,
        CancellationToken ct)
    {
        // Bounded lookup: scan the most recent tenants for a ContactEmail match. In practice
        // the matching tenant was just created by staff onboarding, so it lives in the first
        // page. A dedicated repo method "ListByContactEmail" can replace this when it
        // matters; for now this avoids adding a query surface that nothing else needs.
        var recent = await tenants.ListAsync(500, 0, ct).ConfigureAwait(false);
        foreach (var tenant in recent)
        {
            if (string.IsNullOrWhiteSpace(tenant.ContactEmail)) continue;
            if (!string.Equals(
                Account.NormalizeEmail(tenant.ContactEmail!), account.Email, StringComparison.Ordinal))
                continue;
            var existing = await memberships.FindAsync(account.Id, tenant.Id, ct).ConfigureAwait(false);
            if (existing is not null) continue;
            var grant = TenantMembership.Grant(account.Id, tenant.Id, MembershipRole.Owner, null, now);
            await memberships.AddAsync(grant, ct).ConfigureAwait(false);
        }
    }

    private static async Task ConsumePendingInvitationsAsync(
        Account account,
        ITenantInvitationRepository invitations,
        ITenantMembershipRepository memberships,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var pending = await invitations.ListPendingByEmailAsync(account.Email, ct).ConfigureAwait(false);
        foreach (var inv in pending)
        {
            var existing = await memberships.FindAsync(account.Id, inv.TenantId, ct).ConfigureAwait(false);
            if (existing is null)
            {
                var grant = TenantMembership.Grant(
                    account.Id, inv.TenantId, inv.Role, inv.InvitedByAccountId, now);
                await memberships.AddAsync(grant, ct).ConfigureAwait(false);
            }
            inv.Consume(now);
        }
    }
}
