using ShippingOrchestrator.Domain.Abstractions;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Domain.Events;

public sealed record EcommerceConnectionInstalled(
    Guid ConnectionId,
    TenantId TenantId,
    string PlatformCode,
    string ExternalAccountId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record EcommerceConnectionCredentialsRotated(
    Guid ConnectionId,
    TenantId TenantId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record EcommerceConnectionVerified(
    Guid ConnectionId,
    TenantId TenantId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record EcommerceConnectionRejected(
    Guid ConnectionId,
    TenantId TenantId,
    string Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record CarrierAssignmentCreated(
    Guid AssignmentId,
    TenantId TenantId,
    string CarrierCode,
    int Priority,
    DateTimeOffset OccurredAt) : IDomainEvent;
