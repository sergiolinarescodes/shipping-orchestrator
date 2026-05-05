using ShippingOrchestrator.Domain.Abstractions;
using ShippingOrchestrator.Domain.Ingestion;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Domain.Events;

public sealed record IngestionFailureRaised(
    Guid FailureId,
    TenantId TenantId,
    string ConnectorCode,
    string? ExternalOrderId,
    IngestionReasonCode ReasonCode,
    string Message,
    string TenantHint,
    IngestionFailureSeverity Severity,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record IngestionFailureReoccurred(
    Guid FailureId,
    TenantId TenantId,
    IngestionReasonCode ReasonCode,
    string Message,
    string TenantHint,
    int OccurrenceCount,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record IngestionFailureResolved(
    Guid FailureId,
    TenantId TenantId,
    string ResolvedReason,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record IngestionFailureDismissed(
    Guid FailureId,
    TenantId TenantId,
    string DismissedBy,
    DateTimeOffset OccurredAt) : IDomainEvent;
