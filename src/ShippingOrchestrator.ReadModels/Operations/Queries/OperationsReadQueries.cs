using Microsoft.EntityFrameworkCore;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.ReadModels.Abstractions.Operations;
using ShippingOrchestrator.ReadModels.Operations.Persistence;

namespace ShippingOrchestrator.ReadModels.Operations.Queries;

internal sealed class OperationsReadQueries(OperationsReadDbContext db) : IOperationsReadQueries
{
    public async Task<IReadOnlyList<OpsBatchRow>> ListBatchesAsync(
        string? statusFilter, int take, int skip, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        // LEFT JOIN: a batch with a tenant_id that has no matching row in ops_read.tenants
        // is a data-quality issue we want surfaced in the operator console, not silently
        // dropped. The fallback "(orphan: <id>)" name flags it without breaking rendering.
        // Once the FK constraint on the write side is in place, orphans become impossible
        // and this branch ceases to fire — but the resilient join stays as a safety net.
        var query =
            from b in db.Batches.AsNoTracking()
            join t in db.Tenants.AsNoTracking() on b.TenantId equals t.TenantId into ts
            from t in ts.DefaultIfEmpty()
            where statusFilter == null || b.Status == statusFilter
            orderby b.CreatedAt descending
            select new { b, TenantName = t != null ? t.DisplayName : null };

        var rows = await query.Skip(skip).Take(take).ToArrayAsync(ct).ConfigureAwait(false);
        return rows.Select(r => new OpsBatchRow(
            r.b.BatchId,
            new TenantId(r.b.TenantId),
            r.TenantName ?? $"(orphan: {r.b.TenantId:N})",
            r.b.Status,
            r.b.ParcelCount,
            r.b.SuccessCount,
            r.b.FailureCount,
            r.b.CreatedAt,
            r.b.CompletedAt,
            (int)(now - r.b.CreatedAt).TotalMinutes)).ToArray();
    }

    public async Task<IReadOnlyList<OpsShipmentRow>> ListExceptionsAsync(int take, int skip, CancellationToken ct)
    {
        var rows = await db.Shipments
            .AsNoTracking()
            .Where(s => s.Status == "Failed" || s.FailureReason != null)
            .OrderByDescending(s => s.UpdatedAt)
            .Skip(skip).Take(take)
            .ToArrayAsync(ct)
            .ConfigureAwait(false);
        return rows.Select(s => new OpsShipmentRow(
            s.ShipmentId, new TenantId(s.TenantId), s.BatchId, s.Status, s.CarrierCode,
            s.TrackingNumber, s.FailureReason, s.CountryFrom, s.CountryTo, s.CreatedAt, s.UpdatedAt))
            .ToArray();
    }

    public async Task<IReadOnlyList<OpsTenantRow>> ListTenantsAsync(int take, int skip, CancellationToken ct)
    {
        var rows = await db.Tenants
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Skip(skip).Take(take)
            .ToArrayAsync(ct)
            .ConfigureAwait(false);
        return rows.Select(t => new OpsTenantRow(t.TenantId, t.DisplayName, t.Status, t.CreatedAt)).ToArray();
    }

    public async Task<IReadOnlyList<OpsCarrierKpi>> CarrierSuccessRatesAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        var rows = await db.CarrierDailyKpis
            .AsNoTracking()
            .Where(k => k.Date >= fromDate && k.Date <= toDate)
            .OrderBy(k => k.CarrierCode).ThenBy(k => k.Date)
            .ToArrayAsync(ct)
            .ConfigureAwait(false);
        return rows.Select(k =>
        {
            var total = k.SuccessCount + k.FailureCount;
            var rate = total == 0 ? 0.0 : (double)k.SuccessCount / total;
            return new OpsCarrierKpi(k.CarrierCode, k.Date, k.SuccessCount, k.FailureCount, rate);
        }).ToArray();
    }

    public async Task<IReadOnlyList<OpsIngestionFailureRow>> ListIngestionFailuresAsync(
        OpsIngestionFailureFilter filter, CancellationToken ct)
    {
        var query =
            from f in db.IngestionFailures.AsNoTracking()
            join t in db.Tenants.AsNoTracking() on f.TenantId equals t.TenantId into ts
            from t in ts.DefaultIfEmpty()
            where filter.TenantId == null || f.TenantId == filter.TenantId
            where filter.ConnectorCode == null || f.ConnectorCode == filter.ConnectorCode
            where filter.ReasonCode == null || f.ReasonCode == filter.ReasonCode
            where filter.Status == null || f.Status == filter.Status
            where filter.FromUtc == null || f.LastOccurredAt >= filter.FromUtc
            where filter.ToUtc == null || f.LastOccurredAt <= filter.ToUtc
            orderby f.LastOccurredAt descending
            select new { f, TenantName = t != null ? t.DisplayName : null };

        var rows = await query.Skip(filter.Skip).Take(filter.Take).ToArrayAsync(ct).ConfigureAwait(false);
        return rows.Select(r => Project(r.f, r.TenantName)).ToArray();
    }

    public async Task<OpsIngestionFailureRow?> GetIngestionFailureAsync(Guid failureId, CancellationToken ct)
    {
        var row = await (
            from f in db.IngestionFailures.AsNoTracking()
            join t in db.Tenants.AsNoTracking() on f.TenantId equals t.TenantId into ts
            from t in ts.DefaultIfEmpty()
            where f.FailureId == failureId
            select new { f, TenantName = t != null ? t.DisplayName : null }
        ).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return row is null ? null : Project(row.f, row.TenantName);
    }

    public async Task<IReadOnlyList<OpsIngestionFailureStatGroup>> IngestionFailureStatsAsync(
        DateTimeOffset fromUtc, CancellationToken ct)
    {
        // Pivot ingestion failures by (tenant × reason) over the given window so the ops
        // dashboard can spot misuse trends — e.g. one tenant submitting 90% of all
        // MissingShippingAddress failures suggests a workflow problem on their side.
        var grouped = await (
            from f in db.IngestionFailures.AsNoTracking()
            where f.LastOccurredAt >= fromUtc
            group f by new { f.TenantId, f.ReasonCode } into g
            select new
            {
                g.Key.TenantId,
                g.Key.ReasonCode,
                OpenCount = g.Count(x => x.Status == "Open"),
                ResolvedCount = g.Count(x => x.Status == "Resolved"),
                DismissedCount = g.Count(x => x.Status == "Dismissed"),
                LastSeen = g.Max(x => (DateTimeOffset?)x.LastOccurredAt),
            }
        ).ToArrayAsync(ct).ConfigureAwait(false);

        if (grouped.Length == 0) return Array.Empty<OpsIngestionFailureStatGroup>();

        var tenantIds = grouped.Select(g => g.TenantId).Distinct().ToArray();
        var tenantMap = await db.Tenants.AsNoTracking()
            .Where(t => tenantIds.Contains(t.TenantId))
            .ToDictionaryAsync(t => t.TenantId, t => t.DisplayName, ct)
            .ConfigureAwait(false);

        return grouped
            .OrderByDescending(g => g.OpenCount)
            .ThenByDescending(g => g.LastSeen)
            .Select(g => new OpsIngestionFailureStatGroup(
                g.TenantId,
                tenantMap.GetValueOrDefault(g.TenantId, $"(orphan: {g.TenantId:N})"),
                g.ReasonCode,
                g.OpenCount,
                g.ResolvedCount,
                g.DismissedCount,
                g.LastSeen))
            .ToArray();
    }

    private static OpsIngestionFailureRow Project(OpsIngestionFailureEntity f, string? tenantName) => new(
        f.FailureId,
        f.TenantId,
        tenantName ?? $"(orphan: {f.TenantId:N})",
        f.ConnectorCode,
        f.ExternalOrderId,
        f.ReasonCode,
        f.Status,
        f.Severity,
        f.Message,
        f.TenantHint,
        f.OccurredAt,
        f.LastOccurredAt,
        f.OccurrenceCount,
        f.ResolvedAt,
        f.ResolvedReason,
        f.DismissedAt,
        f.DismissedBy);
}
