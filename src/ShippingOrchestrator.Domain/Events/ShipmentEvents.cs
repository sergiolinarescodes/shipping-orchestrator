using ShippingOrchestrator.Domain.Abstractions;
using ShippingOrchestrator.Domain.Shipments;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Domain.Events;

public sealed record ShipmentBatchAccepted(
    Guid BatchId,
    TenantId TenantId,
    int ItemCount,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ShipmentBatchStartedProcessing(
    Guid BatchId,
    TenantId TenantId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ShipmentBatchCompleted(
    Guid BatchId,
    TenantId TenantId,
    ShipmentBatchStatus FinalStatus,
    int SuccessCount,
    int FailureCount,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ShipmentCreated(
    Guid ShipmentId,
    TenantId TenantId,
    Guid? BatchId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ShipmentCarrierSelected(
    Guid ShipmentId,
    TenantId TenantId,
    string CarrierCode,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ShipmentLabeled(
    Guid ShipmentId,
    TenantId TenantId,
    string CarrierCode,
    string TrackingNumber,
    string LabelUri,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ShipmentFailed(
    Guid ShipmentId,
    TenantId TenantId,
    string Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ShipmentCancelled(
    Guid ShipmentId,
    TenantId TenantId,
    string Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ShipmentTrackingUpdated(
    Guid ShipmentId,
    TenantId TenantId,
    IReadOnlyList<Domain.Shipments.ShipmentTrackingUpdate> Events,
    string? CurrentStatus,
    DateTimeOffset OccurredAt) : IDomainEvent;
