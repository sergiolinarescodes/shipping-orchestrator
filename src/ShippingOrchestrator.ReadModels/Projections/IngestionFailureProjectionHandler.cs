using ShippingOrchestrator.Domain.Events;
using ShippingOrchestrator.Domain.Ingestion;
using ShippingOrchestrator.ReadModels.Customer.Persistence;
using ShippingOrchestrator.ReadModels.Operations.Persistence;
using ShippingOrchestrator.ReadModels.Realtime;

namespace ShippingOrchestrator.ReadModels.Projections;

/// <summary>
/// Fans <see cref="IngestionFailure"/> domain events into BOTH the customer read schema (for
/// the tenant "Needs attention" dashboard) and the ops read schema (for the internal failure
/// panel + (tenant × reason) stats card). Both writes happen in the same handler scope so
/// audiences stay in sync without a second hop. Each event carries enough state for the
/// tenant rows; ops rows additionally store severity + audit fields they need for triage.
/// Persistence + the dashboard fan-out funnel through
/// <see cref="ProjectionSaveExtensions.SaveAndBroadcastAsync"/>.
/// </summary>
public static class IngestionFailureProjectionHandler
{
    public static async Task Handle(
        IngestionFailureRaised @event,
        CustomerReadDbContext customer,
        OperationsReadDbContext ops,
        IDashboardBroadcaster broadcaster,
        CancellationToken ct)
    {
        var customerExisting = await customer.IngestionFailures.FindAsync([@event.FailureId], ct).ConfigureAwait(false);
        if (customerExisting is null)
        {
            customer.IngestionFailures.Add(new CustomerIngestionFailureEntity
            {
                FailureId = @event.FailureId,
                TenantId = @event.TenantId.Value,
                ConnectorCode = @event.ConnectorCode,
                ExternalOrderId = @event.ExternalOrderId,
                ReasonCode = @event.ReasonCode.ToString(),
                Status = IngestionFailureStatus.Open.ToString(),
                Message = @event.Message,
                TenantHint = @event.TenantHint,
                OccurredAt = @event.OccurredAt,
                LastOccurredAt = @event.OccurredAt,
                OccurrenceCount = 1,
            });
        }
        else
        {
            // Replay safety — projection rebuilds re-deliver Raised. Refresh state in case
            // the read model was previously truncated and the row pre-existed.
            customerExisting.Status = IngestionFailureStatus.Open.ToString();
            customerExisting.ReasonCode = @event.ReasonCode.ToString();
            customerExisting.Message = @event.Message;
            customerExisting.TenantHint = @event.TenantHint;
            customerExisting.LastOccurredAt = @event.OccurredAt;
            customerExisting.ResolvedAt = null;
            customerExisting.DismissedAt = null;
        }

        var opsExisting = await ops.IngestionFailures.FindAsync([@event.FailureId], ct).ConfigureAwait(false);
        if (opsExisting is null)
        {
            ops.IngestionFailures.Add(new OpsIngestionFailureEntity
            {
                FailureId = @event.FailureId,
                TenantId = @event.TenantId.Value,
                ConnectorCode = @event.ConnectorCode,
                ExternalOrderId = @event.ExternalOrderId,
                ReasonCode = @event.ReasonCode.ToString(),
                Status = IngestionFailureStatus.Open.ToString(),
                Severity = @event.Severity.ToString(),
                Message = @event.Message,
                TenantHint = @event.TenantHint,
                OccurredAt = @event.OccurredAt,
                LastOccurredAt = @event.OccurredAt,
                OccurrenceCount = 1,
            });
        }
        else
        {
            opsExisting.Status = IngestionFailureStatus.Open.ToString();
            opsExisting.ReasonCode = @event.ReasonCode.ToString();
            opsExisting.Severity = @event.Severity.ToString();
            opsExisting.Message = @event.Message;
            opsExisting.TenantHint = @event.TenantHint;
            opsExisting.LastOccurredAt = @event.OccurredAt;
            opsExisting.ResolvedAt = null;
            opsExisting.ResolvedReason = null;
            opsExisting.DismissedAt = null;
            opsExisting.DismissedBy = null;
        }

        await ProjectionSaveExtensions.SaveAndBroadcastAsync(
            ops, customer, broadcaster, @event.TenantId.Value, DashboardArea.NeedsAttention, ct);
    }

    public static async Task Handle(
        IngestionFailureReoccurred @event,
        CustomerReadDbContext customer,
        OperationsReadDbContext ops,
        IDashboardBroadcaster broadcaster,
        CancellationToken ct)
    {
        var customerRow = await customer.IngestionFailures.FindAsync([@event.FailureId], ct).ConfigureAwait(false);
        if (customerRow is not null)
        {
            customerRow.ReasonCode = @event.ReasonCode.ToString();
            customerRow.Message = @event.Message;
            customerRow.TenantHint = @event.TenantHint;
            customerRow.OccurrenceCount = @event.OccurrenceCount;
            customerRow.LastOccurredAt = @event.OccurredAt;
            customerRow.Status = IngestionFailureStatus.Open.ToString();
        }

        var opsRow = await ops.IngestionFailures.FindAsync([@event.FailureId], ct).ConfigureAwait(false);
        if (opsRow is not null)
        {
            opsRow.ReasonCode = @event.ReasonCode.ToString();
            opsRow.Message = @event.Message;
            opsRow.TenantHint = @event.TenantHint;
            opsRow.OccurrenceCount = @event.OccurrenceCount;
            opsRow.LastOccurredAt = @event.OccurredAt;
            opsRow.Status = IngestionFailureStatus.Open.ToString();
        }

        await ProjectionSaveExtensions.SaveAndBroadcastAsync(
            ops, customer, broadcaster, @event.TenantId.Value, DashboardArea.NeedsAttention, ct);
    }

    public static async Task Handle(
        IngestionFailureResolved @event,
        CustomerReadDbContext customer,
        OperationsReadDbContext ops,
        IDashboardBroadcaster broadcaster,
        CancellationToken ct)
    {
        var customerRow = await customer.IngestionFailures.FindAsync([@event.FailureId], ct).ConfigureAwait(false);
        if (customerRow is not null)
        {
            customerRow.Status = IngestionFailureStatus.Resolved.ToString();
            customerRow.ResolvedAt = @event.OccurredAt;
        }

        var opsRow = await ops.IngestionFailures.FindAsync([@event.FailureId], ct).ConfigureAwait(false);
        if (opsRow is not null)
        {
            opsRow.Status = IngestionFailureStatus.Resolved.ToString();
            opsRow.ResolvedAt = @event.OccurredAt;
            opsRow.ResolvedReason = @event.ResolvedReason;
        }

        await ProjectionSaveExtensions.SaveAndBroadcastAsync(
            ops, customer, broadcaster, @event.TenantId.Value, DashboardArea.NeedsAttention, ct);
    }

    public static async Task Handle(
        IngestionFailureDismissed @event,
        CustomerReadDbContext customer,
        OperationsReadDbContext ops,
        IDashboardBroadcaster broadcaster,
        CancellationToken ct)
    {
        var customerRow = await customer.IngestionFailures.FindAsync([@event.FailureId], ct).ConfigureAwait(false);
        if (customerRow is not null)
        {
            customerRow.Status = IngestionFailureStatus.Dismissed.ToString();
            customerRow.DismissedAt = @event.OccurredAt;
        }

        var opsRow = await ops.IngestionFailures.FindAsync([@event.FailureId], ct).ConfigureAwait(false);
        if (opsRow is not null)
        {
            opsRow.Status = IngestionFailureStatus.Dismissed.ToString();
            opsRow.DismissedAt = @event.OccurredAt;
            opsRow.DismissedBy = @event.DismissedBy;
        }

        await ProjectionSaveExtensions.SaveAndBroadcastAsync(
            ops, customer, broadcaster, @event.TenantId.Value, DashboardArea.NeedsAttention, ct);
    }
}
