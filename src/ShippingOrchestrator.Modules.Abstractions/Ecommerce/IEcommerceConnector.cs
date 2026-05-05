using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Modules.Abstractions.Ecommerce;

public interface IEcommerceConnector
{
    string PlatformCode { get; }

    /// <summary>Returns the OAuth authorization URL the tenant should be redirected to.</summary>
    Task<OAuthInstallUrl> BuildInstallUrlAsync(OAuthInstallRequest request, CancellationToken ct);

    /// <summary>Completes the OAuth handshake and returns the encrypted credentials payload.</summary>
    Task<OAuthInstallResult> CompleteOAuthAsync(OAuthCallback callback, CancellationToken ct);

    /// <summary>Validates and parses an inbound webhook payload.</summary>
    Task<WebhookHandled> HandleWebhookAsync(RawWebhook webhook, CancellationToken ct);

    /// <summary>Pushes a fulfillment update (tracking number, carrier) back to the platform.</summary>
    Task PushFulfillmentAsync(TenantId tenantId, FulfillmentUpdate update, CancellationToken ct);
}
