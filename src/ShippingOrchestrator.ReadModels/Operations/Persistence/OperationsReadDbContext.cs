using Microsoft.EntityFrameworkCore;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.ReadModels.Operations.Persistence;

/// <summary>
/// Internal/admin read schema. Most ops endpoints intentionally cross tenants (the staff
/// "all failures" panel, KPI rollups), so the filter is INERT by default — staff requests
/// run with no tenant in <see cref="ITenantContext"/> and see everything. The filter only
/// kicks in if a future ops endpoint scopes itself to a single tenant via the ambient
/// context, in which case the same defense-in-depth applies as on the customer side.
/// </summary>
public sealed class OperationsReadDbContext(
    DbContextOptions<OperationsReadDbContext> options,
    TenantQueryFilter tenantFilter) : DbContext(options)
{
    public const string SchemaName = "ops_read";

    private readonly TenantQueryFilter _tenantFilter = tenantFilter;

    public DbSet<OpsBatchEntity> Batches => Set<OpsBatchEntity>();
    public DbSet<OpsShipmentEntity> Shipments => Set<OpsShipmentEntity>();
    public DbSet<OpsCarrierDailyKpiEntity> CarrierDailyKpis => Set<OpsCarrierDailyKpiEntity>();
    public DbSet<OpsTenantEntity> Tenants => Set<OpsTenantEntity>();
    public DbSet<OpsOnboardingProcessEntity> OnboardingProcesses => Set<OpsOnboardingProcessEntity>();
    public DbSet<OpsShipmentTrackingEventEntity> ShipmentTrackingEvents => Set<OpsShipmentTrackingEventEntity>();
    public DbSet<OpsIngestionFailureEntity> IngestionFailures => Set<OpsIngestionFailureEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        // Customer + Operations now share the assembly after the read-side merge, so the
        // configuration sweep is scoped to this DbContext's namespace to keep schemas apart.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(OperationsReadDbContext).Assembly,
            t => t.Namespace == typeof(OperationsReadDbContext).Namespace);

        // OpsShipmentTrackingEventEntity has no TenantId column on the ops side — it's
        // joined to its shipment, which is filtered. KPI rollups are cross-tenant by design.
        modelBuilder.Entity<OpsBatchEntity>().HasQueryFilter(
            e => _tenantFilter.IsAnonymous || e.TenantId == _tenantFilter.RequiredTenantGuid);
        modelBuilder.Entity<OpsShipmentEntity>().HasQueryFilter(
            e => _tenantFilter.IsAnonymous || e.TenantId == _tenantFilter.RequiredTenantGuid);
        modelBuilder.Entity<OpsIngestionFailureEntity>().HasQueryFilter(
            e => _tenantFilter.IsAnonymous || e.TenantId == _tenantFilter.RequiredTenantGuid);

        base.OnModelCreating(modelBuilder);
    }
}
