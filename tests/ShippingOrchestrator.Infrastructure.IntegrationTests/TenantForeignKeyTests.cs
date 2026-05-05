using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NUnit.Framework;
using ShippingOrchestrator.Domain.Connections;
using ShippingOrchestrator.Domain.Shipments;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Domain.ValueObjects;
using ShippingOrchestrator.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace ShippingOrchestrator.Infrastructure.IntegrationTests;

/// <summary>
/// Pins the database-level tenant FK contract introduced by the
/// <c>AddTenantForeignKeys</c> migration. The intent is that a row in
/// <c>shipment_batches</c> or <c>ecommerce_connections</c> referencing a tenant id with no
/// matching <c>tenants</c> row must be rejected by Postgres — closing the historical hole
/// where SPA-side stale state (a localStorage tenant id surviving a DB wipe) silently created
/// orphan rows the operator console could not render. If a future migration drops the FK or
/// the model snapshot drifts, these tests fail loudly instead of letting the broken state ship.
/// </summary>
[TestFixture]
[NonParallelizable]
public class TenantForeignKeyTests
{
    private PostgreSqlContainer _postgres = null!;
    private DbContextOptions<OrchestratorDbContext> _options = null!;

    [OneTimeSetUp]
    public async Task SetUp()
    {
#pragma warning disable CS0618
        _postgres = new PostgreSqlBuilder()
#pragma warning restore CS0618
            .WithImage("postgres:17-alpine")
            .Build();
        await _postgres.StartAsync();

        _options = new DbContextOptionsBuilder<OrchestratorDbContext>()
            .UseNpgsql(_postgres.GetConnectionString(), n =>
                n.MigrationsHistoryTable("__ef_migrations_history", OrchestratorDbContext.SchemaName))
            .Options;

        await using var db = new OrchestratorDbContext(_options, TenantQueryFilter.Anonymous);
        await db.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task TearDown() => await _postgres.DisposeAsync();

    [Test]
    public async Task ShipmentBatch_insert_with_unknown_tenant_id_is_rejected_by_fk_constraint()
    {
        var orphanTenant = TenantId.New();
        var batch = ShipmentBatch.Accept(
            orphanTenant,
            IdempotencyKey.Parse("orphan-key-fk-1"),
            shipmentIds: [Guid.NewGuid()],
            DateTimeOffset.UtcNow);

        await using var db = new OrchestratorDbContext(_options, TenantQueryFilter.Anonymous);
        db.ShipmentBatches.Add(batch);

        var act = () => db.SaveChangesAsync();
        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        var pg = ex.Which.InnerException.Should().BeOfType<PostgresException>().Subject;
        pg.SqlState.Should().Be("23503", "missing-tenant inserts must surface as a Postgres FK violation");
        pg.Message.Should().Contain("FK_shipment_batches_tenants_tenant_id");
    }

    [Test]
    public async Task EcommerceConnection_insert_with_unknown_tenant_id_is_rejected_by_fk_constraint()
    {
        var orphanTenant = TenantId.New();
        var connection = EcommerceConnection.Install(
            orphanTenant, "shopify", "orphan-shop.myshopify.com", [0x01], DateTimeOffset.UtcNow);

        await using var db = new OrchestratorDbContext(_options, TenantQueryFilter.Anonymous);
        db.EcommerceConnections.Add(connection);

        var act = () => db.SaveChangesAsync();
        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        var pg = ex.Which.InnerException.Should().BeOfType<PostgresException>().Subject;
        pg.SqlState.Should().Be("23503");
        pg.Message.Should().Contain("FK_ecommerce_connections_tenants_tenant_id");
    }

    [Test]
    public async Task ShipmentBatch_insert_succeeds_when_tenant_row_exists_first()
    {
        var now = DateTimeOffset.UtcNow;
        var tenant = Tenant.Create("FK Sanity", "ops@fk.test", now);
        var batch = ShipmentBatch.Accept(
            tenant.Id,
            IdempotencyKey.Parse("sanity-key-fk-2"),
            shipmentIds: [Guid.NewGuid()],
            now);

        await using var db = new OrchestratorDbContext(_options, TenantQueryFilter.Anonymous);
        db.Tenants.Add(tenant);
        db.ShipmentBatches.Add(batch);
        await db.SaveChangesAsync();

        var roundTripped = await db.ShipmentBatches.AsNoTracking().SingleAsync(b => b.Id == batch.Id);
        roundTripped.TenantId.Should().Be(tenant.Id);
    }
}
