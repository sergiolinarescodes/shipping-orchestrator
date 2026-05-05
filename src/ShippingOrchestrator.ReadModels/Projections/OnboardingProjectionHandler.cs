using ShippingOrchestrator.Domain.Events;
using ShippingOrchestrator.ReadModels.Operations.Persistence;

namespace ShippingOrchestrator.ReadModels.Projections;

/// <summary>
/// Wolverine subscribers that fan onboarding domain events into <c>ops_read.onboarding_processes</c>
/// so the internal dashboard can list in-flight + completed onboarding flows. Customer read does
/// not carry onboarding rows — onboarding is a staff/admin concept.
/// </summary>
public static class OnboardingProjectionHandler
{
    public static async Task Handle(
        OnboardingProcessStarted @event,
        OperationsReadDbContext ops,
        CancellationToken ct)
    {
        var existing = await ops.OnboardingProcesses.FindAsync([@event.ProcessId.Value], ct).ConfigureAwait(false);
        if (existing is not null) return;
        ops.OnboardingProcesses.Add(new OpsOnboardingProcessEntity
        {
            ProcessId = @event.ProcessId.Value,
            FlowCode = @event.FlowCode,
            Status = "InProgress",
            CreatedAt = @event.OccurredAt,
            UpdatedAt = @event.OccurredAt,
        });
        await ops.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public static async Task Handle(
        OnboardingStepAwaiting @event,
        OperationsReadDbContext ops,
        CancellationToken ct)
    {
        var row = await ops.OnboardingProcesses.FindAsync([@event.ProcessId.Value], ct).ConfigureAwait(false);
        if (row is null) return;
        row.CurrentStepCode = @event.StepCode;
        row.UpdatedAt = @event.OccurredAt;
        await ops.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public static async Task Handle(
        OnboardingStepCompleted @event,
        OperationsReadDbContext ops,
        CancellationToken ct)
    {
        var row = await ops.OnboardingProcesses.FindAsync([@event.ProcessId.Value], ct).ConfigureAwait(false);
        if (row is null) return;
        row.CurrentStepCode = @event.StepCode;
        row.UpdatedAt = @event.OccurredAt;
        await ops.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public static async Task Handle(
        OnboardingStepFailed @event,
        OperationsReadDbContext ops,
        CancellationToken ct)
    {
        var row = await ops.OnboardingProcesses.FindAsync([@event.ProcessId.Value], ct).ConfigureAwait(false);
        if (row is null) return;
        row.CurrentStepCode = @event.StepCode;
        row.UpdatedAt = @event.OccurredAt;
        await ops.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public static async Task Handle(
        OnboardingProcessCompleted @event,
        OperationsReadDbContext ops,
        CancellationToken ct)
    {
        var row = await ops.OnboardingProcesses.FindAsync([@event.ProcessId.Value], ct).ConfigureAwait(false);
        if (row is null) return;
        row.Status = "Completed";
        row.TenantId = @event.TenantId?.Value;
        row.CompletedAt = @event.OccurredAt;
        row.UpdatedAt = @event.OccurredAt;
        await ops.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public static async Task Handle(
        OnboardingProcessCancelled @event,
        OperationsReadDbContext ops,
        CancellationToken ct)
    {
        var row = await ops.OnboardingProcesses.FindAsync([@event.ProcessId.Value], ct).ConfigureAwait(false);
        if (row is null) return;
        row.Status = "Cancelled";
        row.CompletedAt = @event.OccurredAt;
        row.UpdatedAt = @event.OccurredAt;
        await ops.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public static async Task Handle(
        OnboardingProcessTimedOut @event,
        OperationsReadDbContext ops,
        CancellationToken ct)
    {
        var row = await ops.OnboardingProcesses.FindAsync([@event.ProcessId.Value], ct).ConfigureAwait(false);
        if (row is null) return;
        row.Status = "TimedOut";
        row.CompletedAt = @event.OccurredAt;
        row.UpdatedAt = @event.OccurredAt;
        await ops.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
