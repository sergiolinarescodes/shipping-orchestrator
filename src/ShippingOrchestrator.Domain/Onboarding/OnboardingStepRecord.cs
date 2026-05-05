using System.Text.Json;

namespace ShippingOrchestrator.Domain.Onboarding;

/// <summary>
/// One slot in an <see cref="OnboardingProcess"/>. Owned by the aggregate, keyed by
/// <c>(ProcessId, Code)</c>. The aggregate enforces ordering and idempotency; this entity is
/// a state container with audit fields. Step semantics (which command runs, how to render)
/// live on the descriptor in the Application layer — Domain only knows codes and statuses.
/// </summary>
public sealed class OnboardingStepRecord
{
    public Guid Id { get; private set; }
    public OnboardingProcessId ProcessId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public int Sequence { get; private set; }
    public OnboardingStepStatus Status { get; private set; }
    public JsonDocument? CollectedPayload { get; private set; }
    public JsonDocument? ResultPayload { get; private set; }
    public string? FailureReason { get; private set; }
    public string? ExternalCorrelationId { get; private set; }
    public DateTimeOffset? AwaitingExpiresAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private OnboardingStepRecord() { }

    internal static OnboardingStepRecord Pending(OnboardingProcessId processId, string code, int sequence) => new()
    {
        Id = Guid.NewGuid(),
        ProcessId = processId,
        Code = code,
        Sequence = sequence,
        Status = OnboardingStepStatus.Pending,
    };

    internal void MarkAwaiting(
        string externalCorrelationId,
        DateTimeOffset? expiresAt,
        JsonDocument? collectedPayload,
        JsonDocument? resultPayload,
        DateTimeOffset now)
    {
        Status = OnboardingStepStatus.Awaiting;
        ExternalCorrelationId = externalCorrelationId;
        AwaitingExpiresAt = expiresAt;
        if (collectedPayload is not null) CollectedPayload = collectedPayload;
        if (resultPayload is not null) ResultPayload = resultPayload;
        StartedAt ??= now;
    }

    internal void MarkCompleted(JsonDocument? collectedPayload, JsonDocument? resultPayload, DateTimeOffset now)
    {
        Status = OnboardingStepStatus.Completed;
        if (collectedPayload is not null) CollectedPayload = collectedPayload;
        if (resultPayload is not null) ResultPayload = resultPayload;
        StartedAt ??= now;
        CompletedAt = now;
        FailureReason = null;
    }

    internal void MarkFailed(string reason, JsonDocument? collectedPayload, DateTimeOffset now)
    {
        Status = OnboardingStepStatus.Failed;
        FailureReason = reason;
        if (collectedPayload is not null) CollectedPayload = collectedPayload;
        StartedAt ??= now;
    }

    internal void MarkSkipped(string? reason, DateTimeOffset now)
    {
        Status = OnboardingStepStatus.Skipped;
        FailureReason = reason;
        StartedAt ??= now;
        CompletedAt = now;
    }

    internal void Reset()
    {
        Status = OnboardingStepStatus.Pending;
        CollectedPayload = null;
        ResultPayload = null;
        FailureReason = null;
        ExternalCorrelationId = null;
        AwaitingExpiresAt = null;
        StartedAt = null;
        CompletedAt = null;
    }
}
