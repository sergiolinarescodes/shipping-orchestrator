using ShippingOrchestrator.Domain.Identity;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Application.Identity;

public sealed record SessionAccountView(
    AccountId AccountId,
    string Email,
    string? DisplayName,
    TenantId? CurrentTenantId,
    IReadOnlyList<SessionTenantMembershipView> Tenants);

public sealed record SessionTenantMembershipView(
    TenantId TenantId,
    string DisplayName,
    string Status,
    MembershipRole Role);
