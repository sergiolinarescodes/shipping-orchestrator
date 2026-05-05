using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.ReadModels.Customer.Persistence;
using ShippingOrchestrator.ReadModels.Operations.Persistence;

namespace ShippingOrchestrator.ReadModels.Tests;

/// <summary>
/// Verifies the read-replica indirection that PublicApi and PrivateApi opt into
/// (preferReadReplica: true). The contract: when the host opts in AND a replica connection
/// string is configured, the DbContext binds to the replica; otherwise it falls back to the
/// primary, then to <c>Orchestrator</c>. Worker keeps the writer (preferReadReplica defaults
/// to false) because projection handlers persist through these contexts.
/// </summary>
[TestFixture]
public class ReadConnectionStringResolutionTests
{
    private const string PrimaryHost = "Host=primary.local;Port=5432;Database=shipping_orchestrator;Username=app;Password=p";
    private const string ReplicaHost = "Host=replica.local;Port=5432;Database=shipping_orchestrator;Username=app;Password=p";
    private const string OrchHost    = "Host=orch.local;Port=5432;Database=shipping_orchestrator;Username=app;Password=p";

    [Test]
    public void Customer_PrefersReplica_WhenConfigured_AndOptedIn()
    {
        var ctx = ResolveCustomer(
            new()
            {
                ["ConnectionStrings:Orchestrator"] = OrchHost,
                ["ConnectionStrings:CustomerRead"] = PrimaryHost,
                ["ConnectionStrings:CustomerReadReplica"] = ReplicaHost,
            },
            preferReadReplica: true);

        ctx.Database.GetConnectionString().Should().Contain("replica.local");
    }

    [Test]
    public void Customer_FallsBackToPrimary_WhenReplicaMissing()
    {
        var ctx = ResolveCustomer(
            new()
            {
                ["ConnectionStrings:Orchestrator"] = OrchHost,
                ["ConnectionStrings:CustomerRead"] = PrimaryHost,
            },
            preferReadReplica: true);

        ctx.Database.GetConnectionString().Should().Contain("primary.local");
    }

    [Test]
    public void Customer_UsesPrimary_WhenOptedOut()
    {
        var ctx = ResolveCustomer(
            new()
            {
                ["ConnectionStrings:Orchestrator"] = OrchHost,
                ["ConnectionStrings:CustomerRead"] = PrimaryHost,
                ["ConnectionStrings:CustomerReadReplica"] = ReplicaHost,
            },
            preferReadReplica: false);

        ctx.Database.GetConnectionString().Should().Contain("primary.local");
    }

    [Test]
    public void Customer_FallsBackToOrchestrator_WhenPrimaryMissing()
    {
        var ctx = ResolveCustomer(
            new() { ["ConnectionStrings:Orchestrator"] = OrchHost },
            preferReadReplica: false);

        ctx.Database.GetConnectionString().Should().Contain("orch.local");
    }

    [Test]
    public void Operations_PrefersReplica_WhenConfigured_AndOptedIn()
    {
        var ctx = ResolveOperations(
            new()
            {
                ["ConnectionStrings:Orchestrator"] = OrchHost,
                ["ConnectionStrings:OperationsRead"] = PrimaryHost,
                ["ConnectionStrings:OperationsReadReplica"] = ReplicaHost,
            },
            preferReadReplica: true);

        ctx.Database.GetConnectionString().Should().Contain("replica.local");
    }

    [Test]
    public void Operations_UsesPrimary_WhenOptedOut()
    {
        var ctx = ResolveOperations(
            new()
            {
                ["ConnectionStrings:Orchestrator"] = OrchHost,
                ["ConnectionStrings:OperationsRead"] = PrimaryHost,
                ["ConnectionStrings:OperationsReadReplica"] = ReplicaHost,
            },
            preferReadReplica: false);

        ctx.Database.GetConnectionString().Should().Contain("primary.local");
    }

    private static CustomerReadDbContext ResolveCustomer(
        Dictionary<string, string?> values, bool preferReadReplica)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddSingleton(TenantQueryFilter.Anonymous);
        services.AddCustomerReadPlatform(configuration, preferReadReplica);
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<CustomerReadDbContext>();
    }

    private static OperationsReadDbContext ResolveOperations(
        Dictionary<string, string?> values, bool preferReadReplica)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddSingleton(TenantQueryFilter.Anonymous);
        services.AddOperationsReadPlatform(configuration, preferReadReplica);
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<OperationsReadDbContext>();
    }
}
