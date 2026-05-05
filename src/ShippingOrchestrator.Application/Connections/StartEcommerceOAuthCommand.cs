using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Modules.Abstractions;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;

namespace ShippingOrchestrator.Application.Connections;

public sealed record StartEcommerceOAuthCommand(
    TenantId TenantId,
    string PlatformCode,
    string ExternalAccountId,
    string RedirectUri,
    string State);

public sealed record StartEcommerceOAuthResult(string AuthorizationUrl);

public static class StartEcommerceOAuthHandler
{
    public static async Task<StartEcommerceOAuthResult> Handle(
        StartEcommerceOAuthCommand command,
        ConnectorRegistry registry,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var registration = registry.Get(command.PlatformCode);
        if (registration.Kind != ConnectorKind.Ecommerce)
            throw new InvalidOperationException($"Connector '{command.PlatformCode}' is not an ecommerce connector.");

        var connector = (IEcommerceConnector)registration.ConnectorFactory(serviceProvider);
        var url = await connector.BuildInstallUrlAsync(
                new OAuthInstallRequest(command.TenantId, command.ExternalAccountId, command.RedirectUri, command.State),
                cancellationToken)
            .ConfigureAwait(false);
        return new StartEcommerceOAuthResult(url.AuthorizationUrl);
    }
}
