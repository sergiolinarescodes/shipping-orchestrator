using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShippingOrchestrator.Modules.Abstractions;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;

namespace ShippingOrchestrator.EcommerceConnectors.WooCommerce;

public sealed class WooCommerceConnectorModule : EcommerceConnectorModuleBase
{
    public override string ConnectorCode => "woocommerce";
    public override string DisplayName => "WooCommerce";

    protected override void RegisterEcommerceServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<WooCommerceOptions>().Bind(configuration.GetSection(WooCommerceOptions.SectionName));
        services.AddHttpClient(WooCommerceEcommerceConnector.HttpClientName);
        services.AddScoped<WooCommerceEcommerceConnector>();
        services.AddScoped<WooCommerceInMemoryEcommerceConnector>();

        services.AddSingleton<IEcommerceOrderTranslator, WooCommerceOrderTranslator>();
    }

    protected override IEcommerceConnector ResolveConnector(IServiceProvider services, ConnectorMode mode) => mode switch
    {
        ConnectorMode.InMemory => services.GetRequiredService<WooCommerceInMemoryEcommerceConnector>(),
        ConnectorMode.Real => services.GetRequiredService<WooCommerceEcommerceConnector>(),
        _ => throw new InvalidOperationException($"Unknown WooCommerce mode '{mode}'."),
    };

    protected override IInstallGuideProvider ResolveInstallGuide(IServiceProvider services, ConnectorMode mode) =>
        new WooCommerceInstallGuide(mode);
}
