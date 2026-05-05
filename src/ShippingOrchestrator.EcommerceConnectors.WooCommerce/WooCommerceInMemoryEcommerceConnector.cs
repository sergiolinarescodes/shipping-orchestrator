using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;

namespace ShippingOrchestrator.EcommerceConnectors.WooCommerce;

/// <summary>
/// Local-only WooCommerce simulator. <c>BuildInstallUrlAsync</c> returns a URL pointing at
/// the PublicApi onboarding callback so a "click" lands back at the orchestrator with
/// synthetic creds; <c>CompleteOAuthAsync</c> always succeeds and produces a deterministic
/// credentials bundle so unsigned dev webhooks pass through. Selected by
/// <c>Connectors:WooCommerce:Mode = InMemory</c>.
/// </summary>
public sealed class WooCommerceInMemoryEcommerceConnector(
    IOptions<WooCommerceOptions> options,
    ILogger<WooCommerceInMemoryEcommerceConnector> log) : IEcommerceConnector
{
    public const string DevWebhookSecret = "wc-inmemory-secret";
    public string PlatformCode => "woocommerce";

    public Task<OAuthInstallUrl> BuildInstallUrlAsync(OAuthInstallRequest request, CancellationToken ct)
    {
        var baseUrl = options.Value.OnboardingCallbackBaseUrl.TrimEnd('/');
        // The synthetic URL points at a dev-only "fake-approve" landing page that the SPA
        // serves; in tests we POST directly to /v1/onboarding/callback/woocommerce instead.
        var url = $"{baseUrl}/v1/connections/dashboard-callback/woocommerce/simulate" +
                  $"?store={Uri.EscapeDataString(request.ExternalAccountId)}" +
                  $"&user_id={Uri.EscapeDataString(request.State)}";
        log.LogInformation("WooCommerce (in-memory) install URL synthesized for tenant {Tenant}, store {Store}",
            request.TenantId, request.ExternalAccountId);
        return Task.FromResult(new OAuthInstallUrl(url));
    }

    public Task<OAuthInstallResult> CompleteOAuthAsync(OAuthCallback callback, CancellationToken ct)
    {
        var storeUrl = WooCommerceEcommerceConnector.NormalizeStoreUrl(callback.ExternalAccountId);
        var bundle = JsonSerializer.SerializeToUtf8Bytes(new WooCommerceCredentialsBundle(
            ConsumerKey: $"ck_inmemory_{Guid.NewGuid():N}"[..24],
            ConsumerSecret: $"cs_inmemory_{Guid.NewGuid():N}"[..24],
            StoreUrl: storeUrl,
            WebhookSecret: DevWebhookSecret));
        return Task.FromResult(new OAuthInstallResult(true, storeUrl, bundle));
    }

    public Task<WebhookHandled> HandleWebhookAsync(RawWebhook webhook, CancellationToken ct) =>
        Task.FromResult(new WebhookHandled(true));

    public Task PushFulfillmentAsync(TenantId tenantId, FulfillmentUpdate update, CancellationToken ct)
    {
        log.LogInformation("WooCommerce (in-memory) push fulfillment for tenant {Tenant}, order {OrderId}, tracking {Tracking}",
            tenantId, update.ExternalOrderId, update.TrackingNumber);
        return Task.CompletedTask;
    }
}
