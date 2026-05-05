using System.Text.Json;
using ShippingOrchestrator.Domain.Abstractions;
using ShippingOrchestrator.Domain.Events;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Domain.Onboarding;

/// <summary>
/// Tracks "where in a flow are we?" for one tenant-onboarding attempt. The aggregate is the
/// system of record for orchestration progress; it never duplicates the work of
/// <see cref="Tenant"/>, <see cref="Domain.Connections.EcommerceConnection"/>, or
/// <see cref="Domain.Connections.CarrierAssignment"/> — it records that a step ran and remembers
/// the resulting ids on the step result payload. Multiple flow shapes are supported by
/// hydrating from an <see cref="OnboardingFlowBlueprint"/> when the process starts; the
/// aggregate enforces ordering, idempotency, and "no rewinding past a committed write".
/// </summary>
public sealed class OnboardingProcess : AggregateRoot
{
    private readonly List<OnboardingStepRecord> _steps = [];
    private readonly Dictionary<string, bool> _committedByCode = [];

    public OnboardingProcessId Id { get; private set; }
    public string FlowCode { get; private set; } = string.Empty;
    public OnboardingProcessStatus Status { get; private set; }
    public TenantId? TenantId { get; private set; }
    public string? StartedByStaffUserId { get; private set; }
    public string? ContactEmail { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public IReadOnlyList<OnboardingStepRecord> Steps => _steps;

    private OnboardingProcess() { }

    public static OnboardingProcess Start(
        OnboardingFlowBlueprint blueprint,
        string? startedByStaffUserId,
        string? contactEmail,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        if (blueprint.Steps.Count == 0)
            throw new ArgumentException("Flow must declare at least one step.", nameof(blueprint));

        var process = new OnboardingProcess
        {
            Id = OnboardingProcessId.New(),
            FlowCode = blueprint.FlowCode,
            Status = OnboardingProcessStatus.InProgress,
            StartedByStaffUserId = startedByStaffUserId,
            ContactEmail = contactEmail,
            CreatedAt = now,
            UpdatedAt = now,
        };

        foreach (var s in blueprint.Steps.OrderBy(s => s.Sequence))
        {
            process._steps.Add(OnboardingStepRecord.Pending(process.Id, s.Code, s.Sequence));
            process._committedByCode[s.Code] = s.IsCommitted;
        }

        process.Raise(new OnboardingProcessStarted(process.Id, blueprint.FlowCode, now));
        return process;
    }

    /// <summary>
    /// Re-hydrates the in-memory committed-step lookup for an aggregate loaded from EF — the
    /// blueprint is not stored, so the Application layer hands it back when issuing commands.
    /// </summary>
    public void HydrateBlueprint(OnboardingFlowBlueprint blueprint)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        _committedByCode.Clear();
        foreach (var s in blueprint.Steps)
            _committedByCode[s.Code] = s.IsCommitted;
    }

    public void MarkStepAwaiting(
        string stepCode,
        string externalCorrelationId,
        DateTimeOffset? expiresAt,
        DateTimeOffset now,
        JsonDocument? collectedPayload = null,
        JsonDocument? resultPayload = null)
    {
        EnsureInProgress();
        var step = FindStep(stepCode);
        EnsurePredecessorsResolved(step);
        if (step.Status is OnboardingStepStatus.Completed or OnboardingStepStatus.Skipped)
            return;
        if (step.Status == OnboardingStepStatus.Awaiting && step.ExternalCorrelationId == externalCorrelationId)
            return; // double-arming with same correlation is a no-op
        step.MarkAwaiting(externalCorrelationId, expiresAt, collectedPayload, resultPayload, now);
        UpdatedAt = now;
        Raise(new OnboardingStepAwaiting(Id, stepCode, externalCorrelationId, now));
    }

    public void CompleteStep(
        string stepCode,
        JsonDocument? collectedPayload,
        JsonDocument? resultPayload,
        TenantId? boundTenantId,
        DateTimeOffset now)
    {
        EnsureInProgress();
        var step = FindStep(stepCode);
        EnsurePredecessorsResolved(step);

        if (step.Status == OnboardingStepStatus.Completed)
            return; // idempotent — Wolverine retries land here when the previous attempt succeeded

        step.MarkCompleted(collectedPayload, resultPayload, now);
        if (boundTenantId is { } tid && TenantId is null)
            TenantId = tid;
        UpdatedAt = now;
        Raise(new OnboardingStepCompleted(Id, stepCode, step.Sequence, now));

        if (_steps.All(s => s.Status is OnboardingStepStatus.Completed or OnboardingStepStatus.Skipped))
        {
            Status = OnboardingProcessStatus.Completed;
            CompletedAt = now;
            Raise(new OnboardingProcessCompleted(Id, TenantId, now));
        }
    }

    public void FailStep(string stepCode, string reason, JsonDocument? collectedPayload, DateTimeOffset now)
    {
        EnsureInProgress();
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var step = FindStep(stepCode);
        step.MarkFailed(reason, collectedPayload, now);
        UpdatedAt = now;
        Raise(new OnboardingStepFailed(Id, stepCode, reason, now));
    }

    public void Skip(string stepCode, string? reason, DateTimeOffset now)
    {
        EnsureInProgress();
        var step = FindStep(stepCode);
        EnsurePredecessorsResolved(step);
        step.MarkSkipped(reason, now);
        UpdatedAt = now;
        Raise(new OnboardingStepCompleted(Id, stepCode, step.Sequence, now));
    }

    /// <summary>
    /// Resets every step at sequence &gt;= targetSequence back to Pending, allowing the user to
    /// re-enter input from <paramref name="targetStepCode"/> onwards. Steps marked
    /// <c>IsCommitted = true</c> in the blueprint act as commit boundaries — the caller
    /// cannot rewind to or past a completed committed step (resetting it would orphan the
    /// non-rewindable aggregate it created). Returns the offending step's code if the rewind
    /// is blocked (caller responds with HTTP 409); null on success.
    /// </summary>
    public string? RewindTo(string targetStepCode, DateTimeOffset now)
    {
        EnsureInProgress();
        var target = FindStep(targetStepCode);
        var blockingCommit = _steps
            .Where(s => s.Sequence >= target.Sequence)
            .Where(s => s.Status is OnboardingStepStatus.Completed)
            .Where(s => _committedByCode.TryGetValue(s.Code, out var committed) && committed)
            .OrderBy(s => s.Sequence)
            .FirstOrDefault();
        if (blockingCommit is not null) return blockingCommit.Code;

        foreach (var s in _steps.Where(s => s.Sequence >= target.Sequence))
            s.Reset();
        UpdatedAt = now;
        return null;
    }

    public void Cancel(string? reason, DateTimeOffset now)
    {
        if (Status != OnboardingProcessStatus.InProgress) return;
        Status = OnboardingProcessStatus.Cancelled;
        UpdatedAt = now;
        CompletedAt = now;
        Raise(new OnboardingProcessCancelled(Id, reason, now));
    }

    public void TimeOut(string stepCode, DateTimeOffset now)
    {
        if (Status != OnboardingProcessStatus.InProgress) return;
        var step = FindStep(stepCode);
        if (step.Status != OnboardingStepStatus.Awaiting) return;
        step.MarkFailed("timed out", collectedPayload: null, now);
        Status = OnboardingProcessStatus.TimedOut;
        UpdatedAt = now;
        CompletedAt = now;
        Raise(new OnboardingProcessTimedOut(Id, stepCode, now));
    }

    private void EnsureInProgress()
    {
        if (Status != OnboardingProcessStatus.InProgress)
            throw new InvalidOperationException(
                $"Onboarding process {Id} is {Status}; no further state transitions allowed.");
    }

    private OnboardingStepRecord FindStep(string code) =>
        _steps.FirstOrDefault(s => string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Step '{code}' is not part of process {Id}.");

    private void EnsurePredecessorsResolved(OnboardingStepRecord step)
    {
        var unresolved = _steps
            .Where(s => s.Sequence < step.Sequence)
            .FirstOrDefault(s => s.Status is OnboardingStepStatus.Pending or OnboardingStepStatus.Awaiting or OnboardingStepStatus.Failed);
        if (unresolved is not null)
            throw new InvalidOperationException(
                $"Cannot transition '{step.Code}' (seq {step.Sequence}) while predecessor '{unresolved.Code}' is {unresolved.Status}.");
    }
}
