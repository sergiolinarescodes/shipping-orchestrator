using ShippingOrchestrator.Domain.Abstractions;
using ShippingOrchestrator.Domain.Events;

namespace ShippingOrchestrator.Domain.Identity;

/// <summary>
/// One-time, short-lived sign-in token. Only the SHA-256 hash of the token plaintext is
/// persisted; the plaintext is returned to the caller once at creation and never stored.
/// </summary>
public sealed class MagicLinkToken : AggregateRoot
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public string? IpHash { get; private set; }

    private MagicLinkToken() { }

    public static MagicLinkToken Issue(
        string email,
        string tokenHash,
        DateTimeOffset now,
        TimeSpan ttl,
        string? ipHash = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ttl, TimeSpan.Zero);
        var t = new MagicLinkToken
        {
            Id = Guid.NewGuid(),
            Email = Account.NormalizeEmail(email),
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = now.Add(ttl),
            IpHash = ipHash,
        };
        t.Raise(new MagicLinkRequested(t.Id, t.Email, t.ExpiresAt, now));
        return t;
    }

    public bool IsConsumable(DateTimeOffset now) => ConsumedAt is null && now < ExpiresAt;

    public void Consume(DateTimeOffset now)
    {
        if (ConsumedAt is not null) throw new InvalidOperationException("Token already consumed.");
        if (now >= ExpiresAt) throw new InvalidOperationException("Token expired.");
        ConsumedAt = now;
    }
}
