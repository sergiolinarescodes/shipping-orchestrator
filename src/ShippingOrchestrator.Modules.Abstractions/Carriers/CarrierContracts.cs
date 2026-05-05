using ShippingOrchestrator.Domain.ValueObjects;

namespace ShippingOrchestrator.Modules.Abstractions.Carriers;

public sealed record RateQuoteRequest(
    Address From,
    Address To,
    Parcel Parcel,
    ServiceLevel? PreferredService);

public sealed record RateQuoteOption(
    string CarrierServiceCode,
    ServiceLevel ServiceLevel,
    Money Price,
    TimeSpan? EstimatedTransitTime);

public sealed record RateQuoteResult(
    bool Success,
    IReadOnlyList<RateQuoteOption> Options,
    string? FailureReason = null);

public sealed record LabelCreationRequest(
    Guid ShipmentId,
    Address From,
    Address To,
    Parcel Parcel,
    ServiceLevel? PreferredService,
    string? Reference);

public sealed record LabelCreationResult(
    bool Success,
    string? TrackingNumber,
    string? LabelUri,
    Money? ChargedAmount,
    string? FailureReason = null);

public sealed record TrackingEvent(
    DateTimeOffset OccurredAt,
    string Status,
    string? Description,
    string? Location);

public sealed record TrackingResult(
    bool Found,
    string? CurrentStatus,
    IReadOnlyList<TrackingEvent> Events,
    string? FailureReason = null);

public sealed record CancellationResult(bool Success, string? FailureReason = null);
