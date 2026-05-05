namespace ShippingOrchestrator.Modules.Abstractions.Ecommerce;

/// <summary>
/// Optional capability for an <see cref="IEcommerceConnector"/>: enrich a translated
/// <see cref="EcommerceOrderPayload"/> with data the platform's webhook payload doesn't carry
/// inline. Today the WooCommerce REST order shape doesn't include per-line product weight or
/// dimensions — those live on the product, not the order — so the connector fetches them via
/// the WC REST API after translation. Connectors opt in by implementing this interface
/// alongside <see cref="IEcommerceConnector"/>; callers probe via
/// <c>connector as IEcommerceOrderEnricher</c> and skip the step when not implemented.
/// </summary>
public interface IEcommerceOrderEnricher
{
    Task<EcommerceOrderPayload> EnrichAsync(
        EcommerceOrderPayload payload,
        byte[] decryptedCredentials,
        CancellationToken cancellationToken);
}
