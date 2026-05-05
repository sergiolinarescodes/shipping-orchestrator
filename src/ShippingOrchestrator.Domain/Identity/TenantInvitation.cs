using ShippingOrchestrator.Domain.Abstractions;
using ShippingOrchestrator.Domain.Events;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Domain.Identity;

/// <summary>
/// Pending tenant grant created by an existing <see cref="MembershipRole.Owner"/> for an
/// email that does not yet have a <see cref="TenantMembership"/> on the target tenant. The
/// invitation flushes into a real membership the next time the invitee consumes a magic link.
/// </summary>
public sealed class TenantInvitation : AggregateRoot
{
    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public AccountId InvitedByAccountId { get; private set; }
    public MembershipRole Role { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private TenantInvitation() { }

    public static TenantInvitation Create(
        TenantId tenantId,
        string email,
        AccountId invitedBy,
        MembershipRole role,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        var inv = new TenantInvitation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = Account.NormalizeEmail(email),
            InvitedByAccountId = invitedBy,
            Role = role,
            CreatedAt = now,
        };
        inv.Raise(new TenantInvitationCreated(inv.Id, tenantId, inv.Email, role, now));
        return inv;
    }

    public bool IsPending => ConsumedAt is null && RevokedAt is null;

    public void Consume(DateTimeOffset now)
    {
        if (!IsPending) throw new InvalidOperationException("Invitation not pending.");
        ConsumedAt = now;
    }

    public void Revoke(DateTimeOffset now)
    {
        if (!IsPending) return;
        RevokedAt = now;
    }
}
