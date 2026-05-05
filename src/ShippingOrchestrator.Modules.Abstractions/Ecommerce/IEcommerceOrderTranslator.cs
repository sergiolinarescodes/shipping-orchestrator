using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Modules.Abstractions.Ecommerce;

/// <summary>
/// Maps a connector-native webhook body (Shopify order JSON, future WooCommerce payload, ...)
/// into a normalized <see cref="EcommerceOrderPayload"/>. One implementation per ecommerce
/// connector. Lives in the Abstractions layer so connector projects can implement it without
/// taking a dependency on the Application assembly.
/// </summary>
public interface IEcommerceOrderTranslator
{
    string ConnectorCode { get; }

    Task<EcommerceOrderPayload> TranslateAsync(
        TenantId tenantId,
        string externalAccountId,
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken);
}
