using Microsoft.EntityFrameworkCore;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.ReadModels.Abstractions.Customer;
using ShippingOrchestrator.ReadModels.Customer.Persistence;

namespace ShippingOrchestrator.ReadModels.Customer.Queries;

internal sealed class CustomerReadQueries(CustomerReadDbContext db) : ICustomerReadQueries
{
    public async Task<CustomerBatchView?> GetBatchAsync(TenantId tenantId, Guid batchId, CancellationToken ct)
    {
        var batch = await db.Batches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BatchId == batchId && b.TenantId == tenantId.Value, ct)
            .ConfigureAwait(false);
        if (batch is null) return null;

        var shipments = await db.Shipments
            .AsNoTracking()
            .Where(s => s.BatchId == batchId && s.TenantId == tenantId.Value)
            .OrderBy(s => s.CreatedAt)
            .ToArrayAsync(ct)
            .ConfigureAwait(false);

        return new CustomerBatchView(
            batch.BatchId, new TenantId(batch.TenantId), batch.Status, batch.ParcelCount,
            batch.SuccessCount, batch.FailureCount, batch.CreatedAt, batch.CompletedAt,
            shipments.Select(s => Project(s)).ToArray());
    }

    public async Task<IReadOnlyList<CustomerBatchView>> ListBatchesAsync(
        TenantId tenantId, int take, int skip, string? status, CancellationToken ct)
    {
        var query = db.Batches
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(b => b.Status == status);
        var rows = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip(skip).Take(take)
            .ToArrayAsync(ct)
            .ConfigureAwait(false);
        return rows.Select(b => new CustomerBatchView(
            b.BatchId, new TenantId(b.TenantId), b.Status, b.ParcelCount,
            b.SuccessCount, b.FailureCount, b.CreatedAt, b.CompletedAt,
            Array.Empty<CustomerShipmentView>())).ToArray();
    }

    public async Task<IReadOnlyList<CustomerShipmentView>> ListShipmentsAsync(
        TenantId tenantId, int take, int skip, CancellationToken ct)
    {
        var rows = await db.Shipments
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId.Value)
            .OrderByDescending(s => s.CreatedAt)
            .Skip(skip).Take(take)
            .ToArrayAsync(ct)
            .ConfigureAwait(false);
        return rows.Select(s => Project(s)).ToArray();
    }

    public async Task<CustomerShipmentView?> GetShipmentAsync(TenantId tenantId, Guid shipmentId, CancellationToken ct)
    {
        var row = await db.Shipments
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ShipmentId == shipmentId && s.TenantId == tenantId.Value, ct)
            .ConfigureAwait(false);
        if (row is null) return null;
        var events = await db.ShipmentTrackingEvents
            .AsNoTracking()
            .Where(e => e.ShipmentId == shipmentId && e.TenantId == tenantId.Value)
            .OrderBy(e => e.Sequence)
            .ToArrayAsync(ct)
            .ConfigureAwait(false);
        return Project(row, events.Select(ProjectEvent).ToArray());
    }

    public async Task<IReadOnlyList<CustomerShipmentTrackingEventView>> GetShipmentTimelineAsync(
        TenantId tenantId, Guid shipmentId, CancellationToken ct)
    {
        var rows = await db.ShipmentTrackingEvents
            .AsNoTracking()
            .Where(e => e.ShipmentId == shipmentId && e.TenantId == tenantId.Value)
            .OrderBy(e => e.Sequence)
            .ToArrayAsync(ct)
            .ConfigureAwait(false);
        return rows.Select(ProjectEvent).ToArray();
    }

    private static CustomerShipmentView Project(
        CustomerShipmentEntity e, IReadOnlyList<CustomerShipmentTrackingEventView>? events = null) => new(
        e.ShipmentId, new TenantId(e.TenantId), e.BatchId, e.Status, e.CarrierCode,
        e.TrackingNumber, e.LabelUri, e.FailureReason, e.CreatedAt, e.UpdatedAt, events);

    private static CustomerShipmentTrackingEventView ProjectEvent(CustomerShipmentTrackingEventEntity e) => new(
        e.Sequence, e.EventCode, e.Description, e.Location, e.OccurredAt);

    public async Task<IReadOnlyList<CustomerIngestionFailureView>> ListIngestionFailuresAsync(
        TenantId tenantId, string? status, int take, int skip, CancellationToken ct)
    {
        var query = db.IngestionFailures.AsNoTracking()
            .Where(f => f.TenantId == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(f => f.Status == status);
        var rows = await query
            .OrderByDescending(f => f.LastOccurredAt)
            .Skip(skip).Take(take)
            .ToArrayAsync(ct)
            .ConfigureAwait(false);
        return rows.Select(ProjectFailure).ToArray();
    }

    public async Task<CustomerIngestionFailureView?> GetIngestionFailureAsync(
        TenantId tenantId, Guid failureId, CancellationToken ct)
    {
        var row = await db.IngestionFailures.AsNoTracking()
            .FirstOrDefaultAsync(f => f.FailureId == failureId && f.TenantId == tenantId.Value, ct)
            .ConfigureAwait(false);
        return row is null ? null : ProjectFailure(row);
    }

    public Task<int> CountOpenIngestionFailuresAsync(TenantId tenantId, CancellationToken ct) =>
        db.IngestionFailures.AsNoTracking()
            .CountAsync(f => f.TenantId == tenantId.Value && f.Status == "Open", ct);

    private static CustomerIngestionFailureView ProjectFailure(CustomerIngestionFailureEntity e) => new(
        e.FailureId, new TenantId(e.TenantId), e.ConnectorCode, e.ExternalOrderId, e.ReasonCode,
        e.Status, e.Message, e.TenantHint, e.OccurredAt, e.LastOccurredAt, e.OccurrenceCount,
        e.ResolvedAt, e.DismissedAt);
}
