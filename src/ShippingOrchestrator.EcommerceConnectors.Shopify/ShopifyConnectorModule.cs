using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShippingOrchestrator.Modules.Abstractions;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;

namespace ShippingOrchestrator.EcommerceConnectors.Shopify;

public sealed class ShopifyConnectorModule : EcommerceConnectorModuleBase
{
    public override string ConnectorCode => "shopify";
    public override string DisplayName => "Shopify";

    protected override void RegisterEcommerceServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ShopifyOptions>().Bind(configuration.GetSection(ShopifyOptions.SectionName));
        services.AddHttpClient(ShopifyEcommerceConnector.HttpClientName);
        services.AddScoped<ShopifyEcommerceConnector>();
        services.AddScoped<ShopifyInMemoryEcommerceConnector>();

        // Register the Shopify-specific translator so the central registry can find it. The
        // translator lives in this connector project (anti-corruption layer rule); the
        // application layer only consumes via IEcommerceOrderTranslator. Singleton because
        // ShopifyOrderTranslator is stateless — the registry is itself a singleton.
        services.AddSingleton<IEcommerceOrderTranslator, ShopifyOrderTranslator>();
    }

    protected override IEcommerceConnector ResolveConnector(IServiceProvider services, ConnectorMode mode) => mode switch
    {
        ConnectorMode.InMemory => services.GetRequiredService<ShopifyInMemoryEcommerceConnector>(),
        ConnectorMode.Real => services.GetRequiredService<ShopifyEcommerceConnector>(),
        _ => throw new InvalidOperationException($"Unknown Shopify mode '{mode}'."),
    };

    protected override IInstallGuideProvider ResolveInstallGuide(IServiceProvider services, ConnectorMode mode) =>
        new ShopifyInstallGuide(mode);
}
