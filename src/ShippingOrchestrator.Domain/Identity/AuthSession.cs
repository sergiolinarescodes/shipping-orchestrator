using ShippingOrchestrator.Domain.Abstractions;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Domain.Identity;

/// <summary>
/// Server-side session row backing an HTTP-only cookie. The cookie carries the random
/// session secret; only its SHA-256 hash is persisted in <see cref="SessionHash"/> so a DB
/// dump cannot impersonate users. <see cref="CurrentTenantId"/> is null until the user
/// picks a tenant on the post-sign-in tenant picker.
/// </summary>
public sealed class AuthSession : AggregateRoot
{
    public Guid Id { get; private set; }
    public string SessionHash { get; private set; } = string.Empty;
    public AccountId AccountId { get; private set; }
    public TenantId? CurrentTenantId { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private AuthSession() { }

    public static AuthSession Issue(
        AccountId accountId,
        string sessionHash,
        DateTimeOffset now,
        TimeSpan ttl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionHash);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ttl, TimeSpan.Zero);
        return new AuthSession
        {
            Id = Guid.NewGuid(),
            SessionHash = sessionHash,
            AccountId = accountId,
            IssuedAt = now,
            ExpiresAt = now.Add(ttl),
            LastSeenAt = now,
        };
    }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    /// <summary>
    /// Sliding-expiration touch. Only extends the expiry — never shortens it — so concurrent
    /// requests on the same session cannot accidentally race the row backwards.
    /// </summary>
    public void Touch(DateTimeOffset now, TimeSpan slidingTtl)
    {
        if (!IsActive(now)) throw new InvalidOperationException("Session not active.");
        LastSeenAt = now;
        var newExpiry = now.Add(slidingTtl);
        if (newExpiry > ExpiresAt) ExpiresAt = newExpiry;
    }

    public void SelectTenant(TenantId tenantId, DateTimeOffset now)
    {
        if (!IsActive(now)) throw new InvalidOperationException("Session not active.");
        CurrentTenantId = tenantId;
        LastSeenAt = now;
    }

    public void ClearTenant(DateTimeOffset now)
    {
        if (!IsActive(now)) throw new InvalidOperationException("Session not active.");
        CurrentTenantId = null;
        LastSeenAt = now;
    }

    public void Revoke(DateTimeOffset now)
    {
        if (RevokedAt is not null) return;
        RevokedAt = now;
    }
}
