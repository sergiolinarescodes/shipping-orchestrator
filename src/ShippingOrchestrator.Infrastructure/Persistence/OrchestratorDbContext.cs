using Microsoft.EntityFrameworkCore;
using ShippingOrchestrator.Domain.Connections;
using ShippingOrchestrator.Domain.Identity;
using ShippingOrchestrator.Domain.Ingestion;
using ShippingOrchestrator.Domain.Onboarding;
using ShippingOrchestrator.Domain.Shipments;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Infrastructure.Persistence;

/// <summary>
/// Write-side aggregate root context. Injects <see cref="ITenantContext"/> so
/// <c>OnModelCreating</c> can install global tenant query filters — the captured singleton's
/// <c>Current</c> is read per query (lifted to a SQL parameter), bypassing the filter when no
/// tenant is set so worker/system code paths (projections, sagas, migrations) keep seeing all
/// rows. Pool-safe because <c>ITenantContext</c> is registered as a singleton.
/// </summary>
public sealed class OrchestratorDbContext(
    DbContextOptions<OrchestratorDbContext> options,
    TenantQueryFilter tenantFilter) : DbContext(options)
{
    public const string SchemaName = "orchestrator";

    private readonly TenantQueryFilter _tenantFilter = tenantFilter;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<EcommerceConnection> EcommerceConnections => Set<EcommerceConnection>();
    public DbSet<CarrierAssignment> CarrierAssignments => Set<CarrierAssignment>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentBatch> ShipmentBatches => Set<ShipmentBatch>();
    public DbSet<ShipmentBatchItem> ShipmentBatchItems => Set<ShipmentBatchItem>();
    public DbSet<ShipmentLineage> ShipmentLineages => Set<ShipmentLineage>();
    public DbSet<ShipmentTrackingEvent> ShipmentTrackingEvents => Set<ShipmentTrackingEvent>();
    public DbSet<OnboardingProcess> OnboardingProcesses => Set<OnboardingProcess>();
    public DbSet<PendingEcommerceOrder> PendingEcommerceOrders => Set<PendingEcommerceOrder>();
    public DbSet<IngestionFailure> IngestionFailures => Set<IngestionFailure>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<MagicLinkToken> MagicLinkTokens => Set<MagicLinkToken>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<TenantInvitation> TenantInvitations => Set<TenantInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrchestratorDbContext).Assembly);

        // Tenant isolation — every entity carrying a TenantId gets a global filter
        // bypassed when no tenant is set. Worker projections / system jobs run with
        // a null context and intentionally see across tenants. Onboarding entities are
        // intentionally NOT filtered: their TenantId is nullable until the tenant is
        // provisioned, and admin flows query them across tenants.
        modelBuilder.Entity<EcommerceConnection>().HasQueryFilter(
            e => _tenantFilter.IsAnonymous || e.TenantId == _tenantFilter.RequiredTenant);
        modelBuilder.Entity<CarrierAssignment>().HasQueryFilter(
            e => _tenantFilter.IsAnonymous || e.TenantId == _tenantFilter.RequiredTenant);
        modelBuilder.Entity<Shipment>().HasQueryFilter(
            e => _tenantFilter.IsAnonymous || e.TenantId == _tenantFilter.RequiredTenant);
        modelBuilder.Entity<ShipmentBatch>().HasQueryFilter(
            e => _tenantFilter.IsAnonymous || e.TenantId == _tenantFilter.RequiredTenant);
        modelBuilder.Entity<PendingEcommerceOrder>().HasQueryFilter(
            e => _tenantFilter.IsAnonymous || e.TenantId == _tenantFilter.RequiredTenant);
        modelBuilder.Entity<IngestionFailure>().HasQueryFilter(
            e => _tenantFilter.IsAnonymous || e.TenantId == _tenantFilter.RequiredTenant);
        modelBuilder.Entity<TenantMembership>().HasQueryFilter(
            e => _tenantFilter.IsAnonymous || e.TenantId == _tenantFilter.RequiredTenant);
        modelBuilder.Entity<TenantInvitation>().HasQueryFilter(
            e => _tenantFilter.IsAnonymous || e.TenantId == _tenantFilter.RequiredTenant);

        base.OnModelCreating(modelBuilder);
    }
}
