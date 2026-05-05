using Microsoft.EntityFrameworkCore;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Connections;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Infrastructure.Persistence.Repositories;

internal sealed class EcommerceConnectionRepository(OrchestratorDbContext db) : IEcommerceConnectionRepository
{
    public Task<EcommerceConnection?> FindAsync(Guid connectionId, CancellationToken cancellationToken) =>
        // Bypass the global tenant filter — callers (DisconnectEcommerceConnectionHandler etc.)
        // perform an explicit cross-tenant guard and raise 403 to keep that distinct from a
        // genuine 404. With the filter on, cross-tenant ids would silently look like missing
        // rows and the security signal would degrade.
        db.EcommerceConnections.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken);

    public Task<EcommerceConnection?> FindByExternalAccountAsync(
        TenantId tenantId, string platformCode, string externalAccountId, CancellationToken cancellationToken) =>
        db.EcommerceConnections.FirstOrDefaultAsync(
            c => c.TenantId == tenantId
                 && c.PlatformCode == platformCode
                 && c.ExternalAccountId == externalAccountId,
            cancellationToken);

    public Task<EcommerceConnection?> FindByPlatformAccountAsync(
        string platformCode, string externalAccountId, CancellationToken cancellationToken) =>
        // The (platform, account) pair is NOT globally unique — two tenants can install the
        // same store URL (esp. in dev where everyone points at http://localhost:8080). At
        // webhook time we only have the platform's source header to identify the store, so
        // we must pick a deterministic row. "Most recently installed" is the right tiebreak:
        // it reflects the credentials currently active on the platform side, since each
        // install rotates the platform's webhook secret.
        db.EcommerceConnections
            .Where(c => c.PlatformCode == platformCode && c.ExternalAccountId == externalAccountId)
            .OrderByDescending(c => c.InstalledAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<EcommerceConnection>> ListForTenantAsync(
        TenantId tenantId, CancellationToken cancellationToken) =>
        await db.EcommerceConnections
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.InstalledAt)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(EcommerceConnection connection, CancellationToken cancellationToken) =>
        await db.EcommerceConnections.AddAsync(connection, cancellationToken).ConfigureAwait(false);

    public void Remove(EcommerceConnection connection) =>
        db.EcommerceConnections.Remove(connection);
}
