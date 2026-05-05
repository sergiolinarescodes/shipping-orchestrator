using ShippingOrchestrator.Domain.Abstractions;
using ShippingOrchestrator.Domain.Events;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Domain.Ingestion;

/// <summary>
/// Persistent record of an inbound webhook that could not be translated into a clean
/// <see cref="PendingEcommerceOrder"/>. Tenants see open failures on the customer dashboard
/// "Needs attention" tab; ops see them aggregated by reason. The aggregate is keyed for upsert
/// by <c>(TenantId, ConnectorCode, LookupKey)</c> while <see cref="Status"/> is
/// <see cref="IngestionFailureStatus.Open"/> — the same order failing repeatedly increments
/// <see cref="OccurrenceCount"/> rather than spawning fresh rows.
///
/// LookupKey is normally the platform's <c>ExternalOrderId</c>; when a webhook body cannot
/// even be parsed (no id), we fall back to <c>"hash:" + RawBodyHash</c> so unique-index
/// upsert still works without producing infinite ParseError rows for the same garbled body.
/// </summary>
public sealed class IngestionFailure : AggregateRoot
{
    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string ConnectorCode { get; private set; } = string.Empty;
    public string? ExternalOrderId { get; private set; }
    public string LookupKey { get; private set; } = string.Empty;
    public IngestionReasonCode ReasonCode { get; private set; }
    public IngestionFailureStatus Status { get; private set; }
    public IngestionFailureSeverity Severity { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public string TenantHint { get; private set; } = string.Empty;
    public string RawBodyExcerpt { get; private set; } = string.Empty;
    public string RawBodyHash { get; private set; } = string.Empty;
    public string ContextJson { get; private set; } = "{}";
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset LastOccurredAt { get; private set; }
    public int OccurrenceCount { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public string? ResolvedReason { get; private set; }
    public DateTimeOffset? DismissedAt { get; private set; }
    public string? DismissedBy { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }

    private IngestionFailure() { }

    public static IngestionFailure Raise(
        TenantId tenantId,
        string connectorCode,
        string? externalOrderId,
        IngestionReasonCode reasonCode,
        string message,
        string tenantHint,
        string rawBodyExcerpt,
        string rawBodyHash,
        string? contextJson,
        IngestionFailureSeverity severity,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawBodyHash);

        var lookupKey = string.IsNullOrWhiteSpace(externalOrderId)
            ? $"hash:{rawBodyHash}"
            : externalOrderId;

        var failure = new IngestionFailure
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConnectorCode = connectorCode.ToLowerInvariant(),
            ExternalOrderId = externalOrderId,
            LookupKey = lookupKey,
            ReasonCode = reasonCode,
            Status = IngestionFailureStatus.Open,
            Severity = severity,
            Message = message,
            TenantHint = tenantHint ?? string.Empty,
            RawBodyExcerpt = rawBodyExcerpt ?? string.Empty,
            RawBodyHash = rawBodyHash,
            ContextJson = string.IsNullOrWhiteSpace(contextJson) ? "{}" : contextJson,
            OccurredAt = now,
            LastOccurredAt = now,
            OccurrenceCount = 1,
        };

        failure.Raise(new IngestionFailureRaised(
            failure.Id, tenantId, failure.ConnectorCode, externalOrderId,
            reasonCode, message, failure.TenantHint, severity, now));
        return failure;
    }

    /// <summary>
    /// Records a fresh occurrence on the same lookup key — possibly with a different reason
    /// (e.g. the address is now present but the weight is still zero). Always raises
    /// <see cref="IngestionFailureReoccurred"/>; callers should use
    /// <see cref="BumpOccurrenceCount"/> when the storm-cooldown rule says to stay quiet.
    /// </summary>
    public void Reoccur(
        IngestionReasonCode reasonCode,
        string message,
        string tenantHint,
        string? contextJson,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (Status != IngestionFailureStatus.Open)
            throw new InvalidOperationException($"Cannot reoccur on a {Status} ingestion failure.");

        ReasonCode = reasonCode;
        Message = message;
        TenantHint = tenantHint ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(contextJson))
            ContextJson = contextJson!;
        LastOccurredAt = now;
        OccurrenceCount += 1;
        Raise(new IngestionFailureReoccurred(
            Id, TenantId, reasonCode, message, TenantHint, OccurrenceCount, now));
    }

    /// <summary>
    /// Silent state bump used by <c>RecordIngestionFailureHandler</c>'s 60-second cooldown
    /// path. Updates aggregate state so the row reflects the latest occurrence but emits no
    /// domain event — protects projection write throughput under webhook storms. Projection
    /// counters lag during a storm and resync on the next non-cooldown event.
    /// </summary>
    public void BumpOccurrenceCount(DateTimeOffset now)
    {
        if (Status != IngestionFailureStatus.Open) return;
        LastOccurredAt = now;
        OccurrenceCount += 1;
    }

    public void Resolve(string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status == IngestionFailureStatus.Resolved) return;
        if (Status == IngestionFailureStatus.Dismissed)
            throw new InvalidOperationException("Cannot resolve a dismissed ingestion failure.");
        Status = IngestionFailureStatus.Resolved;
        ResolvedAt = now;
        ResolvedReason = reason;
        ExpiresAt = now.AddDays(30);
        Raise(new IngestionFailureResolved(Id, TenantId, reason, now));
    }

    public void Dismiss(string dismissedBy, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dismissedBy);
        if (Status == IngestionFailureStatus.Dismissed) return;
        if (Status == IngestionFailureStatus.Resolved)
            throw new InvalidOperationException("Cannot dismiss a resolved ingestion failure.");
        Status = IngestionFailureStatus.Dismissed;
        DismissedAt = now;
        DismissedBy = dismissedBy;
        ExpiresAt = now.AddDays(30);
        Raise(new IngestionFailureDismissed(Id, TenantId, dismissedBy, now));
    }
}
