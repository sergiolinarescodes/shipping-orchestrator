using Microsoft.EntityFrameworkCore;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.ReadModels.Customer.Persistence;

/// <summary>
/// Tenant-facing read schema. Injects <see cref="ITenantContext"/> so OnModelCreating can
/// install global query filters — one missing <c>WHERE TenantId = …</c> on a customer-side
/// endpoint would leak another tenant's batches/shipments. Filter bypasses when no tenant is
/// set so projection handlers (Worker host) can write without artificial scoping. Pool-safe
/// because <c>ITenantContext</c> is registered as a singleton.
/// </summary>
public sealed class CustomerReadDbContext(
    DbContextOptions<CustomerReadDbContext> options,
    TenantQueryFilter tenantFilter) : DbContext(options)
{
    public const string SchemaName = "customer_read";

    private readonly TenantQueryFilter _tenantFilter = tenantFilter;

    public DbSet<CustomerBatchEntity> Batches => Set<CustomerBatchEntity>();
    public DbSet<CustomerShipmentEntity> Shipments => Set<CustomerShipmentEntity>();
    public DbSet<CustomerShipmentTrackingEventEntity> ShipmentTrackingEvents => Set<CustomerShipmentTrackingEventEntity>();
    public DbSet<CustomerIngestionFailureEntity> IngestionFailures => Set<CustomerIngestionFailureEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        // Customer + Operations now share the assembly after the read-side merge, so the
        // configuration sweep is scoped to this DbContext's namespace to keep schemas apart.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(CustomerReadDbContext).Assembly,
            t => t.Namespace == typeof(CustomerReadDbContext).Namespace);

        // Defense-in-depth tenant isolation. PublicApi endpoints already filter by tenant
        // explicitly, but a future "forgot to add WHERE" silently leaks across tenants —
        // the filter shuts that door. Bypassed when no tenant is set so projection writes
        // (Worker, system jobs) keep functioning.
        modelBuilder.Entity<CustomerBatchEntity>().HasQueryFilter(
            e => _tenantFilter.IsAnonymous || e.TenantId == _tenantFilter.RequiredTenantGuid);
        modelBuilder.Entity<CustomerShipmentEntity>().HasQueryFilter(
            e => _tenantFilter.IsAnonymous || e.TenantId == _tenantFilter.RequiredTenantGuid);
        modelBuilder.Entity<CustomerShipmentTrackingEventEntity>().HasQueryFilter(
            e => _tenantFilter.IsAnonymous || e.TenantId == _tenantFilter.RequiredTenantGuid);
        modelBuilder.Entity<CustomerIngestionFailureEntity>().HasQueryFilter(
            e => _tenantFilter.IsAnonymous || e.TenantId == _tenantFilter.RequiredTenantGuid);

        base.OnModelCreating(modelBuilder);
    }
}
