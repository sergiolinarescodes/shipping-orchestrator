using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.ReadModels.Abstractions.Customer;

public sealed record CustomerBatchView(
    Guid BatchId,
    TenantId TenantId,
    string Status,
    int ParcelCount,
    int SuccessCount,
    int FailureCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<CustomerShipmentView> Shipments);

public sealed record CustomerShipmentView(
    Guid ShipmentId,
    TenantId TenantId,
    Guid? BatchId,
    string Status,
    string? CarrierCode,
    string? TrackingNumber,
    string? LabelUri,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<CustomerShipmentTrackingEventView>? Events = null);

public sealed record CustomerShipmentTrackingEventView(
    int Sequence,
    string EventCode,
    string? Description,
    string? Location,
    DateTimeOffset OccurredAt);

public sealed record CustomerIngestionFailureView(
    Guid FailureId,
    TenantId TenantId,
    string ConnectorCode,
    string? ExternalOrderId,
    string ReasonCode,
    string Status,
    string Message,
    string TenantHint,
    DateTimeOffset OccurredAt,
    DateTimeOffset LastOccurredAt,
    int OccurrenceCount,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? DismissedAt);

public interface ICustomerReadQueries
{
    Task<CustomerBatchView?> GetBatchAsync(TenantId tenantId, Guid batchId, CancellationToken ct);
    Task<IReadOnlyList<CustomerBatchView>> ListBatchesAsync(
        TenantId tenantId, int take, int skip, string? status, CancellationToken ct);
    Task<IReadOnlyList<CustomerShipmentView>> ListShipmentsAsync(TenantId tenantId, int take, int skip, CancellationToken ct);
    Task<CustomerShipmentView?> GetShipmentAsync(TenantId tenantId, Guid shipmentId, CancellationToken ct);
    Task<IReadOnlyList<CustomerShipmentTrackingEventView>> GetShipmentTimelineAsync(
        TenantId tenantId, Guid shipmentId, CancellationToken ct);
    Task<IReadOnlyList<CustomerIngestionFailureView>> ListIngestionFailuresAsync(
        TenantId tenantId, string? status, int take, int skip, CancellationToken ct);
    Task<CustomerIngestionFailureView?> GetIngestionFailureAsync(
        TenantId tenantId, Guid failureId, CancellationToken ct);
    Task<int> CountOpenIngestionFailuresAsync(TenantId tenantId, CancellationToken ct);
}
