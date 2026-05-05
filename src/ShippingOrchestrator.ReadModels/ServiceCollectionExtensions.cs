using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShippingOrchestrator.ReadModels.Abstractions.Customer;
using ShippingOrchestrator.ReadModels.Abstractions.Operations;
using ShippingOrchestrator.ReadModels.Customer.Persistence;
using ShippingOrchestrator.ReadModels.Customer.Queries;
using ShippingOrchestrator.ReadModels.Operations.Persistence;
using ShippingOrchestrator.ReadModels.Operations.Queries;

namespace ShippingOrchestrator.ReadModels;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the customer read DbContext + queries.
    /// When <paramref name="preferReadReplica"/> is true and a <c>ConnectionStrings:CustomerReadReplica</c>
    /// is configured, the DbContext binds to the replica; otherwise it falls back to the primary
    /// (<c>CustomerRead</c> → <c>Orchestrator</c>). Worker hosts must keep the default
    /// (<paramref name="preferReadReplica"/> = false) because projection handlers write through this
    /// context — replicas are read-only and would reject the inserts. PublicApi (queries only) opts
    /// in so its hot read path can drain a replica without saturating the writer.
    /// </summary>
    public static IServiceCollection AddCustomerReadPlatform(
        this IServiceCollection services, IConfiguration configuration, bool preferReadReplica = false)
    {
        var connectionString = ResolveConnectionString(
            configuration, preferReadReplica,
            replicaKey: "CustomerReadReplica",
            primaryKey: "CustomerRead",
            fallbackKey: "Orchestrator");

        services.AddDbContextPool<CustomerReadDbContext>(opt =>
        {
            opt.UseNpgsql(connectionString, n =>
                n.MigrationsHistoryTable("__ef_migrations_history", CustomerReadDbContext.SchemaName));
        });

        services.AddScoped<ICustomerReadQueries, CustomerReadQueries>();
        return services;
    }

    /// <summary>
    /// Registers the operations read DbContext + queries.
    /// When <paramref name="preferReadReplica"/> is true and <c>ConnectionStrings:OperationsReadReplica</c>
    /// is configured, the DbContext binds to the replica; otherwise primary
    /// (<c>OperationsRead</c> → <c>Orchestrator</c>). PrivateApi opts in for read-mostly ops
    /// dashboard queries; Worker keeps the writer because projections persist through this context.
    /// </summary>
    public static IServiceCollection AddOperationsReadPlatform(
        this IServiceCollection services, IConfiguration configuration, bool preferReadReplica = false)
    {
        var connectionString = ResolveConnectionString(
            configuration, preferReadReplica,
            replicaKey: "OperationsReadReplica",
            primaryKey: "OperationsRead",
            fallbackKey: "Orchestrator");

        services.AddDbContextPool<OperationsReadDbContext>(opt =>
        {
            opt.UseNpgsql(connectionString, n =>
                n.MigrationsHistoryTable("__ef_migrations_history", OperationsReadDbContext.SchemaName));
        });

        services.AddScoped<IOperationsReadQueries, OperationsReadQueries>();
        services.AddScoped<IOpsOnboardingQueries, OpsOnboardingQueries>();
        return services;
    }

    private static string ResolveConnectionString(
        IConfiguration configuration, bool preferReadReplica,
        string replicaKey, string primaryKey, string fallbackKey)
    {
        if (preferReadReplica)
        {
            var replica = configuration.GetConnectionString(replicaKey);
            if (!string.IsNullOrWhiteSpace(replica))
                return replica;
        }
        return configuration.GetConnectionString(primaryKey)
            ?? configuration.GetConnectionString(fallbackKey)
            ?? throw new InvalidOperationException(
                $"ConnectionStrings:{primaryKey} (or fallback ConnectionStrings:{fallbackKey}) is required.");
    }
}
