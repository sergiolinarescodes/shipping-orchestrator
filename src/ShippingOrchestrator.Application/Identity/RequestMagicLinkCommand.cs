using Microsoft.Extensions.Options;
using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Email;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Application.Identity.Templates;
using ShippingOrchestrator.Domain.Identity;
using Wolverine;

namespace ShippingOrchestrator.Application.Identity;

public sealed record RequestMagicLinkCommand(string Email, string? IpHash);

/// <summary>
/// Result is intentionally void/empty: the endpoint always returns 202 regardless of whether
/// an email was sent, to prevent account enumeration. The caller must not branch on whether
/// a token was created.
/// </summary>
public sealed record RequestMagicLinkResult;

public static class RequestMagicLinkHandler
{
    public static async Task<RequestMagicLinkResult> Handle(
        RequestMagicLinkCommand command,
        IAccountRepository accounts,
        ITenantRepository tenants,
        IMagicLinkTokenRepository tokens,
        ITenantInvitationRepository invitations,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        IOptions<AuthOptions> options,
        IClock clock,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Email);

        var email = Account.NormalizeEmail(command.Email);
        var now = clock.UtcNow;

        // Eligibility check: only mail a link if the email matches an existing account, a
        // tenant ContactEmail, or a pending invitation. Unknown emails get a no-op + the same
        // 202 response so attackers cannot enumerate registered accounts.
        var account = await accounts.FindByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        var pendingInvites = await invitations.ListPendingByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        var hasTenantContact = false;
        if (account is null && pendingInvites.Count == 0)
        {
            // Only scan tenants when there is no faster signal. ListAsync is bounded by the
            // page argument; in practice this stays cheap because ContactEmail matches are
            // expected to land via the new-account-bootstrap flow shortly after staff
            // onboarding, so the relevant tenant is usually one of the most recent rows.
            var recentTenants = await tenants.ListAsync(200, 0, cancellationToken).ConfigureAwait(false);
            hasTenantContact = recentTenants.Any(t =>
                !string.IsNullOrWhiteSpace(t.ContactEmail)
                && string.Equals(Account.NormalizeEmail(t.ContactEmail!), email, StringComparison.Ordinal));
        }

        if (account is null && pendingInvites.Count == 0 && !hasTenantContact)
        {
            return new RequestMagicLinkResult();
        }

        var rawToken = TokenGenerator.NewRawSecret();
        var tokenHash = TokenGenerator.Hash(rawToken);
        var ttl = TimeSpan.FromSeconds(options.Value.MagicLinkTtlSeconds);
        var token = MagicLinkToken.Issue(email, tokenHash, now, ttl, command.IpHash);
        await tokens.AddAsync(token, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var link = BuildLink(options.Value.VerifyEndpointBaseUrl, rawToken);
        var message = MagicLinkEmail.Build(email, link, token.ExpiresAt);
        await bus.PublishAsync(new SendEmailCommand(message)).ConfigureAwait(false);

        return new RequestMagicLinkResult();
    }

    private static string BuildLink(string baseUrl, string rawToken)
    {
        var trimmed = baseUrl.TrimEnd('/');
        return $"{trimmed}/v1/auth/verify?token={Uri.EscapeDataString(rawToken)}";
    }
}
