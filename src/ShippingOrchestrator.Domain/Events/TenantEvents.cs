using ShippingOrchestrator.Domain.Abstractions;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Domain.Events;

public sealed record TenantCreated(
    TenantId TenantId,
    string DisplayName,
    TenantStatus Status,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record TenantStatusChanged(
    TenantId TenantId,
    TenantStatus NewStatus,
    DateTimeOffset OccurredAt,
    string? Reason = null) : IDomainEvent;

public sealed record TenantCarrierModeSelected(
    TenantId TenantId,
    TenantCarrierMode CarrierMode,
    ToSAcceptance? ToSAcceptance,
    DateTimeOffset OccurredAt) : IDomainEvent;
