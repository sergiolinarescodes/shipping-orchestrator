using ShippingOrchestrator.Domain.Events;
using ShippingOrchestrator.ReadModels.Customer.Persistence;
using ShippingOrchestrator.ReadModels.Operations.Persistence;
using ShippingOrchestrator.ReadModels.Realtime;

namespace ShippingOrchestrator.ReadModels.Projections;

/// <summary>
/// Wolverine subscribers that fan domain events into both <c>ops_read</c> (internal-business
/// shape) and <c>customer_read</c> (tenant-facing shape) inside a single handler scope.
/// Writing both schemas in the same handler keeps audiences in sync without a second hop.
/// Persistence + the dashboard fan-out funnel through
/// <see cref="ProjectionSaveExtensions.SaveAndBroadcastAsync"/> — see that helper for the
/// retry-convergence reasoning behind the ops-first ordering.
/// </summary>
public static class ShipmentProjectionHandler
{
    public static async Task Handle(
        TenantCreated @event,
        OperationsReadDbContext ops,
        CancellationToken ct)
    {
        var existing = await ops.Tenants.FindAsync([@event.TenantId.Value], ct).ConfigureAwait(false);
        if (existing is null)
        {
            ops.Tenants.Add(new OpsTenantEntity
            {
                TenantId = @event.TenantId.Value,
                DisplayName = @event.DisplayName,
                Status = @event.Status.ToString(),
                CreatedAt = @event.OccurredAt,
            });
        }
        else
        {
            existing.DisplayName = @event.DisplayName;
            existing.Status = @event.Status.ToString();
        }
        await ops.SaveChangesAsync(ct).ConfigureAwait(false);
        // Customer read carries no tenant directory — clients only see their own data.
    }

    public static async Task Handle(
        TenantStatusChanged @event,
        OperationsReadDbContext ops,
        CancellationToken ct)
    {
        var tenant = await ops.Tenants.FindAsync([@event.TenantId.Value], ct).ConfigureAwait(false);
        if (tenant is null) return;
        tenant.Status = @event.NewStatus.ToString();
        await ops.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public static async Task Handle(
        ShipmentBatchAccepted @event,
        OperationsReadDbContext ops,
        CustomerReadDbContext customer,
        IDashboardBroadcaster broadcaster,
        CancellationToken ct)
    {
        ops.Batches.Add(new OpsBatchEntity
        {
            BatchId = @event.BatchId,
            TenantId = @event.TenantId.Value,
            Status = "Pending",
            ParcelCount = @event.ItemCount,
            CreatedAt = @event.OccurredAt,
        });
        customer.Batches.Add(new CustomerBatchEntity
        {
            BatchId = @event.BatchId,
            TenantId = @event.TenantId.Value,
            Status = "Pending",
            ParcelCount = @event.ItemCount,
            CreatedAt = @event.OccurredAt,
        });
        await ProjectionSaveExtensions.SaveAndBroadcastAsync(
            ops, customer, broadcaster, @event.TenantId.Value, DashboardArea.Batches, ct);
    }

    public static async Task Handle(
        ShipmentBatchStartedProcessing @event,
        OperationsReadDbContext ops,
        CustomerReadDbContext customer,
        IDashboardBroadcaster broadcaster,
        CancellationToken ct)
    {
        await UpdateBatchStatus(@event.BatchId, "Processing", null, null, null, ops, customer, ct).ConfigureAwait(false);
        await ProjectionSaveExtensions.SaveAndBroadcastAsync(
            ops, customer, broadcaster, @event.TenantId.Value, DashboardArea.Batches, ct);
    }

    public static async Task Handle(
        ShipmentBatchCompleted @event,
        OperationsReadDbContext ops,
        CustomerReadDbContext customer,
        IDashboardBroadcaster broadcaster,
        CancellationToken ct)
    {
        await UpdateBatchStatus(
                @event.BatchId, @event.FinalStatus.ToString(),
                @event.SuccessCount, @event.FailureCount, @event.OccurredAt,
                ops, customer, ct)
            .ConfigureAwait(false);
        await ProjectionSaveExtensions.SaveAndBroadcastAsync(
            ops, customer, broadcaster, @event.TenantId.Value, DashboardArea.Batches, ct);
    }

    public static async Task Handle(
        ShipmentCreated @event,
        OperationsReadDbContext ops,
        CustomerReadDbContext customer,
        IDashboardBroadcaster broadcaster,
        CancellationToken ct)
    {
        ops.Shipments.Add(new OpsShipmentEntity
        {
            ShipmentId = @event.ShipmentId,
            TenantId = @event.TenantId.Value,
            BatchId = @event.BatchId,
            Status = "Created",
            CountryFrom = string.Empty,
            CountryTo = string.Empty,
            CreatedAt = @event.OccurredAt,
            UpdatedAt = @event.OccurredAt,
        });
        customer.Shipments.Add(new CustomerShipmentEntity
        {
            ShipmentId = @event.ShipmentId,
            TenantId = @event.TenantId.Value,
            BatchId = @event.BatchId,
            Status = "Created",
            CreatedAt = @event.OccurredAt,
            UpdatedAt = @event.OccurredAt,
        });
        await ProjectionSaveExtensions.SaveAndBroadcastAsync(
            ops, customer, broadcaster, @event.TenantId.Value, DashboardArea.Orders, ct);
    }

    public static async Task Handle(
        ShipmentCarrierSelected @event,
        OperationsReadDbContext ops,
        CustomerReadDbContext customer,
        IDashboardBroadcaster broadcaster,
        CancellationToken ct)
    {
        await UpdateShipment(@event.ShipmentId, @event.OccurredAt, ops, customer,
                "CarrierSelected", @event.CarrierCode, null, null, null, ct)
            .ConfigureAwait(false);
        await ProjectionSaveExtensions.SaveAndBroadcastAsync(
            ops, customer, broadcaster, @event.TenantId.Value, DashboardArea.Shipments, ct);
    }

    public static async Task Handle(
        ShipmentLabeled @event,
        OperationsReadDbContext ops,
        CustomerReadDbContext customer,
        IDashboardBroadcaster broadcaster,
        CancellationToken ct)
    {
        await UpdateShipment(@event.ShipmentId, @event.OccurredAt, ops, customer,
                "Labeled", @event.CarrierCode, @event.TrackingNumber, @event.LabelUri, null, ct)
            .ConfigureAwait(false);
        await BumpKpi(@event.CarrierCode, success: true, durationMs: 0, ops, ct).ConfigureAwait(false);
        await ProjectionSaveExtensions.SaveAndBroadcastAsync(
            ops, customer, broadcaster, @event.TenantId.Value, DashboardArea.Shipments, ct);
    }

    public static async Task Handle(
        ShipmentFailed @event,
        OperationsReadDbContext ops,
        CustomerReadDbContext customer,
        IDashboardBroadcaster broadcaster,
        CancellationToken ct)
    {
        await UpdateShipment(@event.ShipmentId, @event.OccurredAt, ops, customer,
                "Failed", null, null, null, @event.Reason, ct)
            .ConfigureAwait(false);
        await ProjectionSaveExtensions.SaveAndBroadcastAsync(
            ops, customer, broadcaster, @event.TenantId.Value, DashboardArea.Shipments, ct);
    }

    private static async Task UpdateBatchStatus(
        Guid batchId, string status, int? success, int? failure, DateTimeOffset? completedAt,
        OperationsReadDbContext ops, CustomerReadDbContext customer, CancellationToken ct)
    {
        var opsRow = await ops.Batches.FindAsync([batchId], ct).ConfigureAwait(false);
        if (opsRow is not null)
        {
            opsRow.Status = status;
            if (success is not null) opsRow.SuccessCount = success.Value;
            if (failure is not null) opsRow.FailureCount = failure.Value;
            if (completedAt is not null) opsRow.CompletedAt = completedAt;
        }

        var customerRow = await customer.Batches.FindAsync([batchId], ct).ConfigureAwait(false);
        if (customerRow is not null)
        {
            customerRow.Status = status;
            if (success is not null) customerRow.SuccessCount = success.Value;
            if (failure is not null) customerRow.FailureCount = failure.Value;
            if (completedAt is not null) customerRow.CompletedAt = completedAt;
        }
    }

    private static async Task UpdateShipment(
        Guid shipmentId,
        DateTimeOffset updatedAt,
        OperationsReadDbContext ops,
        CustomerReadDbContext customer,
        string status,
        string? carrierCode,
        string? trackingNumber,
        string? labelUri,
        string? failureReason,
        CancellationToken ct)
    {
        var opsRow = await ops.Shipments.FindAsync([shipmentId], ct).ConfigureAwait(false);
        if (opsRow is not null)
        {
            opsRow.Status = status;
            opsRow.UpdatedAt = updatedAt;
            if (carrierCode is not null) opsRow.CarrierCode = carrierCode;
            if (trackingNumber is not null) opsRow.TrackingNumber = trackingNumber;
            if (failureReason is not null) opsRow.FailureReason = failureReason;
        }

        var customerRow = await customer.Shipments.FindAsync([shipmentId], ct).ConfigureAwait(false);
        if (customerRow is not null)
        {
            customerRow.Status = status;
            customerRow.UpdatedAt = updatedAt;
            if (carrierCode is not null) customerRow.CarrierCode = carrierCode;
            if (trackingNumber is not null) customerRow.TrackingNumber = trackingNumber;
            if (labelUri is not null) customerRow.LabelUri = labelUri;
            if (failureReason is not null) customerRow.FailureReason = failureReason;
        }
    }

    private static async Task BumpKpi(string carrierCode, bool success, double durationMs,
        OperationsReadDbContext ops, CancellationToken ct)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var kpi = await ops.CarrierDailyKpis.FindAsync([carrierCode, date], ct).ConfigureAwait(false);
        if (kpi is null)
        {
            kpi = new OpsCarrierDailyKpiEntity { CarrierCode = carrierCode, Date = date };
            ops.CarrierDailyKpis.Add(kpi);
        }
        if (success) kpi.SuccessCount++; else kpi.FailureCount++;
        kpi.TotalLabelDurationMs += durationMs;
    }
}
