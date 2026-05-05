using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;

namespace ShippingOrchestrator.Modules.Abstractions;

/// <summary>
/// Mirror of <see cref="CarrierConnectorModuleBase"/> for ecommerce connectors. Selects the
/// real or in-memory adapter via <c>Connectors:{ConnectorCode}:Mode</c> and rejects
/// <see cref="ConnectorMode.InMemory"/> when running in Production. Concrete modules supply
/// the per-mode resolver.
/// </summary>
public abstract class EcommerceConnectorModuleBase : IConnectorModule
{
    public abstract string ConnectorCode { get; }
    public ConnectorKind Kind => ConnectorKind.Ecommerce;
    public virtual string DisplayName => ConnectorCode;

    protected abstract void RegisterEcommerceServices(IServiceCollection services, IConfiguration configuration);
    protected abstract IEcommerceConnector ResolveConnector(IServiceProvider services, ConnectorMode mode);

    /// <summary>
    /// Optional tenant-facing install guide. Override to expose the short checklist + form
    /// fields rendered by the customer SPA's connect modal. Default returns null — the
    /// dashboard then falls back to a generic "click to connect" flow without a guide.
    /// </summary>
    protected virtual IInstallGuideProvider? ResolveInstallGuide(IServiceProvider services, ConnectorMode mode) => null;

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection($"Connectors:{ConnectorCode}");
        services.Configure<ConnectorModeOptions>(ConnectorCode, section);
        RegisterEcommerceServices(services, configuration);
    }

    public void RegisterWithRegistry(ConnectorRegistry registry, IServiceProvider services)
    {
        var mode = ReadMode(services);
        registry.Register(new ConnectorRegistration
        {
            ConnectorCode = ConnectorCode,
            Kind = Kind,
            DisplayName = $"{DisplayName} ({mode.ToString().ToLowerInvariant()})",
            ConnectorFactory = sp => ResolveConnector(sp, mode),
            InstallGuideFactory = sp => ResolveInstallGuide(sp, mode)!,
            Mode = mode,
            Metadata = new Dictionary<string, string> { ["mode"] = mode.ToString().ToLowerInvariant() },
        });
    }

    public Task BootstrapAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var mode = ReadMode(services);
        var environment = services.GetService<IHostEnvironment>();
        if (mode == ConnectorMode.InMemory && environment is not null && environment.IsProduction())
            throw new InvalidOperationException(
                $"Connectors:{ConnectorCode}:Mode=InMemory is not allowed when ASPNETCORE_ENVIRONMENT=Production.");
        return Task.CompletedTask;
    }

    private ConnectorMode ReadMode(IServiceProvider services) =>
        services.GetRequiredService<IOptionsMonitor<ConnectorModeOptions>>().Get(ConnectorCode).Mode;
}
