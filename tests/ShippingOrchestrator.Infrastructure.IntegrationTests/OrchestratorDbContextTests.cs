using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using ShippingOrchestrator.Domain.Connections;
using ShippingOrchestrator.Domain.Shipments;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Domain.ValueObjects;
using ShippingOrchestrator.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace ShippingOrchestrator.Infrastructure.IntegrationTests;

/// <summary>
/// Round-trips every aggregate through Postgres via Testcontainers. Catches mapping
/// regressions that the schema-only model snapshot can't see — e.g. the <c>CountryCode[]</c>
/// → <c>text[]</c> issue that broke the E2E suite originally.
/// </summary>
[TestFixture]
[NonParallelizable]
public class OrchestratorDbContextTests
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
    public async Task Tenant_round_trips_through_postgres()
    {
        var now = DateTimeOffset.UtcNow;
        var tenant = Tenant.Create("Acme NL", "ops@acme.test", now);

        await using (var db = new OrchestratorDbContext(_options, TenantQueryFilter.Anonymous))
        {
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
        }

        await using var read = new OrchestratorDbContext(_options, TenantQueryFilter.Anonymous);
        var loaded = await read.Tenants.SingleAsync(t => t.Id == tenant.Id);
        loaded.DisplayName.Should().Be("Acme NL");
        loaded.Status.Should().Be(TenantStatus.Onboarding);
    }

    [Test]
    public async Task CarrierAssignment_round_trips_country_arrays_as_text_arrays()
    {
        var tenant = TenantId.New();
        var now = DateTimeOffset.UtcNow;
        var assignment = CarrierAssignment.Create(tenant, "postnl", priority: 100,
            origins: [new CountryCode("NL"), new CountryCode("BE")],
            destinations: [new CountryCode(CountryCode.Wildcard)],
            now);

        await using (var db = new OrchestratorDbContext(_options, TenantQueryFilter.Anonymous))
        {
            db.CarrierAssignments.Add(assignment);
            await db.SaveChangesAsync();
        }

        await using var read = new OrchestratorDbContext(_options, TenantQueryFilter.Anonymous);
        var loaded = await read.CarrierAssignments.SingleAsync(a => a.Id == assignment.Id);
        loaded.OriginCountries.Should().BeEquivalentTo(["NL", "BE"]);
        loaded.DestinationCountries.Should().BeEquivalentTo(["*"]);
        loaded.Covers(new CountryCode("NL"), new CountryCode("DE")).Should().BeTrue();
        loaded.Covers(new CountryCode("DE"), new CountryCode("NL")).Should().BeFalse();
    }

    [Test]
    public async Task Shipment_round_trips_address_and_parcel_as_jsonb()
    {
        var tenant = TenantId.New();
        var now = DateTimeOffset.UtcNow;
        var batchId = Guid.NewGuid();
        var shipment = Shipment.Create(
            tenant,
            batchId,
            from: new Address("Acme", "Hoofdstraat 1", null, "Amsterdam", null, "1012 AB", new CountryCode("NL")),
            to: new Address("Customer", "Rue 1", null, "Brussels", null, "1000", new CountryCode("BE")),
            parcel: new Parcel(Weight.FromGrams(750), new Dimension(200, 200, 100), new Money(24.99m, "EUR")),
            preferredService: ServiceLevel.Standard,
            now);

        await using (var db = new OrchestratorDbContext(_options, TenantQueryFilter.Anonymous))
        {
            db.Shipments.Add(shipment);
            await db.SaveChangesAsync();
        }

        await using var read = new OrchestratorDbContext(_options, TenantQueryFilter.Anonymous);
        var loaded = await read.Shipments.SingleAsync(s => s.Id == shipment.Id);
        loaded.From.City.Should().Be("Amsterdam");
        loaded.To.City.Should().Be("Brussels");
        loaded.From.Country.Value.Should().Be("NL");
        loaded.Parcel.Weight.Grams.Should().Be(750);
        loaded.Parcel.DeclaredValue.Currency.Should().Be("EUR");
        loaded.PreferredService.Should().Be(ServiceLevel.Standard);
    }

    [Test]
    public async Task ShipmentBatch_round_trips_with_items_via_idempotency_key_lookup()
    {
        var now = DateTimeOffset.UtcNow;
        var tenantEntity = Tenant.Create("Batch Tenant", "ops@batch.test", now);
        var tenant = tenantEntity.Id;
        var idempotencyKey = IdempotencyKey.Parse("test-key-abcd1234");
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var batch = ShipmentBatch.Accept(tenant, idempotencyKey, ids, now);

        await using (var db = new OrchestratorDbContext(_options, TenantQueryFilter.Anonymous))
        {
            db.Tenants.Add(tenantEntity);
            db.ShipmentBatches.Add(batch);
            await db.SaveChangesAsync();
        }

        await using var read = new OrchestratorDbContext(_options, TenantQueryFilter.Anonymous);
        IdempotencyKey? wrapped = idempotencyKey;
        var loaded = await read.ShipmentBatches.Include(b => b.Items)
            .SingleAsync(b => b.IdempotencyKey == wrapped && b.TenantId == tenant);
        loaded.Items.Should().HaveCount(2);
        loaded.Items.Select(i => i.ShipmentId).Should().BeEquivalentTo(ids);
        loaded.Status.Should().Be(ShipmentBatchStatus.Pending);
    }
}
