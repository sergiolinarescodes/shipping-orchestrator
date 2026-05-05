using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ShippingOrchestrator.Modules.Abstractions;

/// <summary>
/// Self-contained registration surface for one connector (one ecommerce platform or one
/// carrier). Hosts iterate every registered module instead of calling connector-specific
/// extension methods, so onboarding a new connector requires a new project + a single
/// <see cref="ConnectorModuleExtensions.AddConnectorModule{T}"/> line in each host.
/// PublicApi, PrivateApi, Worker, and shared infrastructure stay agnostic.
/// </summary>
public interface IConnectorModule
{
    /// <summary>Stable identifier — lower-case (e.g. "shopify", "postnl").</summary>
    string ConnectorCode { get; }

    /// <summary>Whether this is an ecommerce-side or carrier-side adapter.</summary>
    ConnectorKind Kind { get; }

    /// <summary>DI registrations the connector needs (HTTP clients, options, services).</summary>
    void RegisterServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Builds and registers the runtime <see cref="ConnectorRegistration"/>.</summary>
    void RegisterWithRegistry(ConnectorRegistry registry, IServiceProvider services);

    /// <summary>
    /// Optional one-shot bootstrap (e.g. ensure storage buckets exist). Runs in every host
    /// after EF migrations apply. Default no-op.
    /// </summary>
    Task BootstrapAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
