using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShippingOrchestrator.Modules.Abstractions;
using ShippingOrchestrator.Modules.Abstractions.Carriers;

namespace ShippingOrchestrator.CarrierConnectors.PostNL;

public sealed class PostNlConnectorModule : CarrierConnectorModuleBase
{
    public override string ConnectorCode => "postnl";

    protected override void RegisterCarrierServices(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection($"Connectors:{ConnectorCode}");
        services.Configure<PostNlRealOptions>(section.GetSection("Real"));
        services.Configure<PostNlInMemoryOptions>(section.GetSection("InMemory"));
        services.AddScoped<PostNlInMemoryCarrierConnector>();
    }

    protected override ICarrierConnector ResolveConnector(IServiceProvider services, ConnectorMode mode) => mode switch
    {
        ConnectorMode.InMemory => services.GetRequiredService<PostNlInMemoryCarrierConnector>(),
        ConnectorMode.Real => throw new NotSupportedException(
            "Real PostNL connector is not implemented yet. Set Connectors:PostNL:Mode=InMemory while the prototype is in flight."),
        _ => throw new InvalidOperationException($"Unknown PostNL mode '{mode}'."),
    };
}
