using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.ReadModels.Abstractions.Operations;

public sealed record OpsBatchRow(
    Guid BatchId,
    TenantId TenantId,
    string TenantDisplayName,
    string Status,
    int ParcelCount,
    int SuccessCount,
    int FailureCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    int AgeingMinutes);

public sealed record OpsShipmentRow(
    Guid ShipmentId,
    TenantId TenantId,
    Guid? BatchId,
    string Status,
    string? CarrierCode,
    string? TrackingNumber,
    string? FailureReason,
    string CountryFrom,
    string CountryTo,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record OpsCarrierKpi(
    string CarrierCode,
    DateOnly Date,
    int SuccessCount,
    int FailureCount,
    double SuccessRate);

public sealed record OpsTenantRow(
    Guid TenantId,
    string DisplayName,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record OpsIngestionFailureRow(
    Guid FailureId,
    Guid TenantId,
    string TenantDisplayName,
    string ConnectorCode,
    string? ExternalOrderId,
    string ReasonCode,
    string Status,
    string Severity,
    string Message,
    string TenantHint,
    DateTimeOffset OccurredAt,
    DateTimeOffset LastOccurredAt,
    int OccurrenceCount,
    DateTimeOffset? ResolvedAt,
    string? ResolvedReason,
    DateTimeOffset? DismissedAt,
    string? DismissedBy);

public sealed record OpsIngestionFailureStatGroup(
    Guid TenantId,
    string TenantDisplayName,
    string ReasonCode,
    int OpenCount,
    int ResolvedCount,
    int DismissedCount,
    DateTimeOffset? LastSeen);

public sealed record OpsIngestionFailureFilter(
    Guid? TenantId = null,
    string? ConnectorCode = null,
    string? ReasonCode = null,
    string? Status = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    int Take = 100,
    int Skip = 0);

public interface IOperationsReadQueries
{
    Task<IReadOnlyList<OpsBatchRow>> ListBatchesAsync(string? statusFilter, int take, int skip, CancellationToken ct);
    Task<IReadOnlyList<OpsShipmentRow>> ListExceptionsAsync(int take, int skip, CancellationToken ct);
    Task<IReadOnlyList<OpsCarrierKpi>> CarrierSuccessRatesAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct);
    Task<IReadOnlyList<OpsTenantRow>> ListTenantsAsync(int take, int skip, CancellationToken ct);
    Task<IReadOnlyList<OpsIngestionFailureRow>> ListIngestionFailuresAsync(
        OpsIngestionFailureFilter filter, CancellationToken ct);
    Task<OpsIngestionFailureRow?> GetIngestionFailureAsync(Guid failureId, CancellationToken ct);
    Task<IReadOnlyList<OpsIngestionFailureStatGroup>> IngestionFailureStatsAsync(
        DateTimeOffset fromUtc, CancellationToken ct);
}
