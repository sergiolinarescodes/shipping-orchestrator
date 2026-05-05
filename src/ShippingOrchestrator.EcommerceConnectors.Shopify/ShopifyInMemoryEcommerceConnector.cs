using Microsoft.Extensions.Logging;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;

namespace ShippingOrchestrator.EcommerceConnectors.Shopify;

/// <summary>
/// Local-only Shopify simulator. <c>BuildInstallUrlAsync</c> echoes the caller-supplied
/// redirect URI back with synthetic query params so dev/E2E flows can "click" it and land at
/// the orchestrator's dashboard callback; <c>CompleteOAuthAsync</c> always succeeds and
/// returns a fixed dev token; webhook HMAC validation is permissive. Mirrors the PostNL
/// in-memory pattern. Selected by <c>Connectors:Shopify:Mode = InMemory</c>.
/// </summary>
public sealed class ShopifyInMemoryEcommerceConnector(
    ILogger<ShopifyInMemoryEcommerceConnector> log) : IEcommerceConnector
{
    public string PlatformCode => "shopify";

    public Task<OAuthInstallUrl> BuildInstallUrlAsync(OAuthInstallRequest request, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RedirectUri);
        var separator = request.RedirectUri.Contains('?') ? '&' : '?';
        var url = $"{request.RedirectUri}{separator}shop={Uri.EscapeDataString(request.ExternalAccountId)}" +
                  $"&code=simulated-code" +
                  $"&state={Uri.EscapeDataString(request.State)}";
        log.LogInformation("Shopify (in-memory) install URL synthesized for tenant {Tenant}, account {Account}",
            request.TenantId, request.ExternalAccountId);
        return Task.FromResult(new OAuthInstallUrl(url));
    }

    public Task<OAuthInstallResult> CompleteOAuthAsync(OAuthCallback callback, CancellationToken ct)
    {
        // The token is encrypted by the OAuth handler before it lands in Postgres; treat the
        // bytes here as a placeholder credential the rest of the pipeline can decrypt.
        var fakeToken = $"shpat_inmemory_{Guid.NewGuid():N}"[..32];
        var payload = System.Text.Encoding.UTF8.GetBytes(fakeToken);
        return Task.FromResult(new OAuthInstallResult(
            Success: true,
            ExternalAccountId: callback.ExternalAccountId,
            CredentialsPayload: payload));
    }

    public Task<WebhookHandled> HandleWebhookAsync(RawWebhook webhook, CancellationToken ct) =>
        Task.FromResult(new WebhookHandled(true));

    public Task PushFulfillmentAsync(TenantId tenantId, FulfillmentUpdate update, CancellationToken ct)
    {
        log.LogInformation("Shopify (in-memory) push fulfillment for tenant {Tenant}, order {OrderId}, tracking {Tracking}",
            tenantId, update.ExternalOrderId, update.TrackingNumber);
        return Task.CompletedTask;
    }
}
