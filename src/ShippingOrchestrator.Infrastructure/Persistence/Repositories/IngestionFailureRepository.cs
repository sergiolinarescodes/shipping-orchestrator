using Microsoft.EntityFrameworkCore;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Ingestion;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Infrastructure.Persistence.Repositories;

internal sealed class IngestionFailureRepository(OrchestratorDbContext db) : IIngestionFailureRepository
{
    public Task<IngestionFailure?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.IngestionFailures.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public Task<IngestionFailure?> FindOpenByExternalOrderIdAsync(
        TenantId tenantId,
        string connectorCode,
        string externalOrderId,
        CancellationToken cancellationToken) =>
        db.IngestionFailures.FirstOrDefaultAsync(
            f => f.TenantId == tenantId
                 && f.ConnectorCode == connectorCode
                 && f.ExternalOrderId == externalOrderId
                 && f.Status == IngestionFailureStatus.Open,
            cancellationToken);

    public Task<IngestionFailure?> FindOpenByLookupKeyAsync(
        TenantId tenantId,
        string connectorCode,
        string lookupKey,
        CancellationToken cancellationToken) =>
        db.IngestionFailures.FirstOrDefaultAsync(
            f => f.TenantId == tenantId
                 && f.ConnectorCode == connectorCode
                 && f.LookupKey == lookupKey
                 && f.Status == IngestionFailureStatus.Open,
            cancellationToken);

    public async Task AddAsync(IngestionFailure failure, CancellationToken cancellationToken) =>
        await db.IngestionFailures.AddAsync(failure, cancellationToken).ConfigureAwait(false);

    public void Remove(IngestionFailure failure) =>
        db.IngestionFailures.Remove(failure);
}
