using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ShippingOrchestrator.Modules.Abstractions;

public static class ConnectorModuleExtensions
{
    /// <summary>
    /// Instantiates the module, registers it as a singleton <see cref="IConnectorModule"/>,
    /// and immediately runs its <see cref="IConnectorModule.RegisterServices"/> so all
    /// connector-owned dependencies land in DI before any consumer resolves them.
    /// </summary>
    public static IServiceCollection AddConnectorModule<TModule>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TModule : class, IConnectorModule, new()
    {
        var module = new TModule();
        services.AddSingleton<IConnectorModule>(module);
        module.RegisterServices(services, configuration);
        return services;
    }

    /// <summary>
    /// Iterates every registered <see cref="IConnectorModule"/> and runs its
    /// <see cref="IConnectorModule.RegisterWithRegistry"/>. Call once at host startup
    /// after the service provider has been built.
    /// </summary>
    public static void RegisterConnectorModules(this IServiceProvider services)
    {
        var registry = services.GetRequiredService<ConnectorRegistry>();
        foreach (var module in services.GetServices<IConnectorModule>())
            module.RegisterWithRegistry(registry, services);
    }

    /// <summary>
    /// Iterates every registered <see cref="IConnectorModule"/> and runs its async
    /// bootstrap. Call once at host startup, after migrations have applied.
    /// </summary>
    public static async Task BootstrapConnectorModulesAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        foreach (var module in services.GetServices<IConnectorModule>())
            await module.BootstrapAsync(services, cancellationToken).ConfigureAwait(false);
    }
}
