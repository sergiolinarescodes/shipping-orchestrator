using ShippingOrchestrator.Domain.Abstractions;
using ShippingOrchestrator.Domain.Identity;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Domain.Events;

public sealed record AccountCreated(
    AccountId AccountId,
    string Email,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record AccountSignedIn(
    AccountId AccountId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record MagicLinkRequested(
    Guid TokenId,
    string Email,
    DateTimeOffset ExpiresAt,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record TenantMembershipGranted(
    Guid MembershipId,
    AccountId AccountId,
    TenantId TenantId,
    MembershipRole Role,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record TenantInvitationCreated(
    Guid InvitationId,
    TenantId TenantId,
    string Email,
    MembershipRole Role,
    DateTimeOffset OccurredAt) : IDomainEvent;
