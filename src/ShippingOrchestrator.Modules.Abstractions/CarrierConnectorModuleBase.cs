using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ShippingOrchestrator.Modules.Abstractions.Carriers;

namespace ShippingOrchestrator.Modules.Abstractions;

/// <summary>
/// Shared scaffolding for carrier connector modules. Handles binding
/// <see cref="ConnectorModeOptions"/> per connector code, registry registration,
/// and the Production guard that rejects <see cref="ConnectorMode.InMemory"/>.
/// Derived modules only declare carrier-specific service registrations and a
/// per-mode resolution switch — the Mode/guard plumbing is not repeated.
/// </summary>
public abstract class CarrierConnectorModuleBase : IConnectorModule
{
    public abstract string ConnectorCode { get; }
    public ConnectorKind Kind => ConnectorKind.Carrier;

    /// <summary>
    /// Register carrier-specific services + bind sub-options
    /// (e.g. <c>Connectors:{ConnectorCode}:Real</c>, <c>:InMemory</c>).
    /// </summary>
    protected abstract void RegisterCarrierServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>
    /// Resolve the concrete <see cref="ICarrierConnector"/> for the configured mode.
    /// Throw if a mode is unsupported (e.g. real adapter not implemented yet).
    /// </summary>
    protected abstract ICarrierConnector ResolveConnector(IServiceProvider services, ConnectorMode mode);

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection($"Connectors:{ConnectorCode}");
        services.Configure<ConnectorModeOptions>(ConnectorCode, section);
        RegisterCarrierServices(services, configuration);
    }

    public void RegisterWithRegistry(ConnectorRegistry registry, IServiceProvider services)
    {
        var mode = ReadMode(services);
        registry.Register(new ConnectorRegistration
        {
            ConnectorCode = ConnectorCode,
            Kind = Kind,
            DisplayName = $"{ConnectorCode} ({mode.ToString().ToLowerInvariant()})",
            ConnectorFactory = sp => ResolveConnector(sp, mode),
            Metadata = new Dictionary<string, string> { ["mode"] = mode.ToString().ToLowerInvariant() },
        });
    }

    public Task BootstrapAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var mode = ReadMode(services);
        var environment = services.GetService<IHostEnvironment>();
        if (mode == ConnectorMode.InMemory && environment is not null && environment.IsProduction())
            throw new InvalidOperationException(
                $"Connectors:{ConnectorCode}:Mode=InMemory is not allowed when ASPNETCORE_ENVIRONMENT=Production. " +
                "Configure the real adapter or run in a non-production environment.");
        return Task.CompletedTask;
    }

    private ConnectorMode ReadMode(IServiceProvider services) =>
        services.GetRequiredService<IOptionsMonitor<ConnectorModeOptions>>().Get(ConnectorCode).Mode;
}
