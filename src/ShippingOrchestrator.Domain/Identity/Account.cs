using ShippingOrchestrator.Domain.Abstractions;
using ShippingOrchestrator.Domain.Events;

namespace ShippingOrchestrator.Domain.Identity;

/// <summary>
/// Identifies a human end-user. Keyed by email (lower-cased, trimmed). Independent of
/// <c>Tenant</c> — one account joins many tenants via <see cref="TenantMembership"/> rows.
/// </summary>
public sealed class Account : AggregateRoot
{
    public AccountId Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastSignInAt { get; private set; }

    private Account() { }

    public static Account Create(string email, DateTimeOffset now, string? displayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        var normalized = NormalizeEmail(email);
        var account = new Account
        {
            Id = AccountId.New(),
            Email = normalized,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            CreatedAt = now,
        };
        account.Raise(new AccountCreated(account.Id, normalized, now));
        return account;
    }

    public void RecordSignIn(DateTimeOffset now)
    {
        LastSignInAt = now;
        Raise(new AccountSignedIn(Id, now));
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
