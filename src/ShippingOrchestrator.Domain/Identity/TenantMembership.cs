using ShippingOrchestrator.Domain.Abstractions;
using ShippingOrchestrator.Domain.Events;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Domain.Identity;

/// <summary>
/// Grants an <see cref="Account"/> access to a <see cref="Tenant"/> with a role. Unique on
/// (AccountId, TenantId).
/// </summary>
public sealed class TenantMembership : AggregateRoot
{
    public Guid Id { get; private set; }
    public AccountId AccountId { get; private set; }
    public TenantId TenantId { get; private set; }
    public MembershipRole Role { get; private set; }
    public AccountId? GrantedByAccountId { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }

    private TenantMembership() { }

    public static TenantMembership Grant(
        AccountId accountId,
        TenantId tenantId,
        MembershipRole role,
        AccountId? grantedBy,
        DateTimeOffset now)
    {
        var m = new TenantMembership
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            TenantId = tenantId,
            Role = role,
            GrantedByAccountId = grantedBy,
            GrantedAt = now,
        };
        m.Raise(new TenantMembershipGranted(m.Id, accountId, tenantId, role, now));
        return m;
    }
}
