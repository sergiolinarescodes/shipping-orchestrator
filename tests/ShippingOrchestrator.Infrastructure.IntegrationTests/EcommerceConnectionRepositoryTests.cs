using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using ShippingOrchestrator.Domain.Connections;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Infrastructure.Persistence;
using ShippingOrchestrator.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace ShippingOrchestrator.Infrastructure.IntegrationTests;

/// <summary>
/// Reverse-lookup invariants for the inbound webhook path. The webhook endpoint cannot
/// disambiguate tenants from a Shopify shop domain or a WC store URL alone, so the
/// repository must pick a deterministic row when the (platform, account) pair is reused
/// across tenants. "Most recently installed" is the load-bearing tiebreak — anything else
/// resolves to a stale per-install secret and every webhook delivery 401s. That regression
/// shipped to prod once; this test is the seatbelt.
/// </summary>
[TestFixture]
[NonParallelizable]
public class EcommerceConnectionRepositoryTests
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
    public async Task FindByPlatformAccountAsync_returns_most_recently_installed_when_two_tenants_share_a_store_url()
    {
        var clock = DateTimeOffset.UtcNow;
        var tenantA = Tenant.Create("Tenant A", null, clock);
        var tenantB = Tenant.Create("Tenant B", null, clock);

        // Same platform+account, different tenants. In dev both tenants commonly point at
        // http://localhost:8080; in prod a merchant could install the app under a tenant,
        // off-board, then on-board into a different tenant — same store URL, two rows.
        var older = EcommerceConnection.Install(tenantA.Id, "woocommerce", "http://localhost:8080",
            credentialsCipher: [0x01], now: clock.AddMinutes(-30));
        var newer = EcommerceConnection.Install(tenantB.Id, "woocommerce", "http://localhost:8080",
            credentialsCipher: [0x02], now: clock.AddMinutes(-1));

        await using (var db = new OrchestratorDbContext(_options, TenantQueryFilter.Anonymous))
        {
            db.Tenants.AddRange(tenantA, tenantB);
            db.EcommerceConnections.AddRange(older, newer);
            await db.SaveChangesAsync();
        }

        await using var read = new OrchestratorDbContext(_options, TenantQueryFilter.Anonymous);
        var repo = new EcommerceConnectionRepository(read);

        var resolved = await repo.FindByPlatformAccountAsync("woocommerce", "http://localhost:8080", CancellationToken.None);
        resolved.Should().NotBeNull();
        resolved!.Id.Should().Be(newer.Id, "the most recently installed connection holds the credentials currently registered on the platform side");
        resolved.TenantId.Should().Be(tenantB.Id);
        resolved.CredentialsCipher.Should().BeEquivalentTo([(byte)0x02]);
    }

    [Test]
    public async Task FindByPlatformAccountAsync_returns_null_when_no_match()
    {
        await using var read = new OrchestratorDbContext(_options, TenantQueryFilter.Anonymous);
        var repo = new EcommerceConnectionRepository(read);
        var resolved = await repo.FindByPlatformAccountAsync("woocommerce", "http://nowhere.example.test", CancellationToken.None);
        resolved.Should().BeNull();
    }
}
