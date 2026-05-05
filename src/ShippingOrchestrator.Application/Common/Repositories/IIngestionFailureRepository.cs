using ShippingOrchestrator.Domain.Ingestion;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Application.Common.Repositories;

public interface IIngestionFailureRepository
{
    Task<IngestionFailure?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Find the single Open failure for a given <c>(tenant, connector, externalOrderId)</c>
    /// triple. Used by the auto-resolve hook in <c>IngestEcommerceOrderHandler</c> when a
    /// successful re-translate arrives. Returns null when there is nothing to resolve.
    /// </summary>
    Task<IngestionFailure?> FindOpenByExternalOrderIdAsync(
        TenantId tenantId,
        string connectorCode,
        string externalOrderId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Find the Open failure by the indexed lookup key — usually <c>ExternalOrderId</c>,
    /// or <c>"hash:" + RawBodyHash</c> when the body was unparseable. The upsert path in
    /// <c>RecordIngestionFailureHandler</c> uses this so a parse-error storm against the
    /// same garbled body coalesces into one row with bumped <c>OccurrenceCount</c>.
    /// </summary>
    Task<IngestionFailure?> FindOpenByLookupKeyAsync(
        TenantId tenantId,
        string connectorCode,
        string lookupKey,
        CancellationToken cancellationToken);

    Task AddAsync(IngestionFailure failure, CancellationToken cancellationToken);
    void Remove(IngestionFailure failure);
}
