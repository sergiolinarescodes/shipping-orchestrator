using System.Text.Json;
using ShippingOrchestrator.Application.Connections;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.PublicApi.Authentication;
using Wolverine;

namespace ShippingOrchestrator.PublicApi.Endpoints;

/// <summary>
/// Anonymous landing endpoints for dashboard-driven (post-onboarding) connection installs.
/// Differs from <see cref="OnboardingPublicEndpoints"/>: the wizard's callback resolves the
/// in-flight OnboardingProcess by state-as-correlation-id, while these resolve a tenant-bound
/// connection install by decoding the signed install state we minted in
/// <see cref="DashboardConnectionsEndpoints"/>. The distinct path lets the two flows coexist
/// without touching each other's contracts.
/// </summary>
public static class DashboardConnectionCallbackEndpoints
{
    public static void MapDashboardConnectionCallbackEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/connections/dashboard-callback").WithTags("Connections (Public Callback)");

        // Shopify GET callback. Shopify sends ?code=X&state=Y where state is our protected token.
        group.MapGet("/shopify", async (
            string code,
            string state,
            string? shop,
            InstallStateProtector protector,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            if (!protector.TryUnprotect(state, out var payload) || payload is null)
                return Results.Unauthorized();

            var externalAccountId = !string.IsNullOrWhiteSpace(shop) ? shop : payload.ExternalAccountId;
            try
            {
                var result = await bus.InvokeAsync<CompleteEcommerceOAuthResult>(
                    new CompleteEcommerceOAuthCommand(
                        new TenantId(payload.TenantId),
                        payload.PlatformCode,
                        externalAccountId,
                        code,
                        state,
                        new Dictionary<string, string>()),
                    ct).ConfigureAwait(false);
                return Results.Content(InstallReceivedHtml("Shopify", result.ConnectionId), "text/html");
            }
            catch (Exception ex)
            {
                return Results.Content(InstallFailedHtml("Shopify", ex.Message), "text/html", statusCode: 500);
            }
        }).AllowAnonymous().WithName("DashboardCallbackShopify");

        // WooCommerce POST callback. WP posts JSON {user_id, consumer_key, consumer_secret,
        // key_id, key_permissions} where user_id is our protected token. We round-trip the
        // creds into AdditionalParameters so WooCommerceEcommerceConnector.CompleteOAuthAsync
        // can unpack them; nothing platform-specific leaks into the application layer.
        group.MapPost("/woocommerce", async (
            HttpContext http,
            InstallStateProtector protector,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            using var doc = await JsonDocument.ParseAsync(http.Request.Body, cancellationToken: ct).ConfigureAwait(false);
            var root = doc.RootElement;

            if (!TryGetString(root, "user_id", out var userId) || string.IsNullOrWhiteSpace(userId))
                return Results.BadRequest(new { error = "user_id is required." });
            if (!protector.TryUnprotect(userId, out var payload) || payload is null)
                return Results.Unauthorized();

            if (!TryGetString(root, "consumer_key", out var consumerKey)
                || !TryGetString(root, "consumer_secret", out var consumerSecret))
                return Results.BadRequest(new { error = "consumer_key and consumer_secret are required." });

            var additional = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["consumer_key"] = consumerKey!,
                ["consumer_secret"] = consumerSecret!,
            };
            if (TryGetString(root, "key_id", out var keyId) && keyId is not null) additional["key_id"] = keyId;
            if (TryGetString(root, "key_permissions", out var perms) && perms is not null) additional["key_permissions"] = perms;

            try
            {
                var result = await bus.InvokeAsync<CompleteEcommerceOAuthResult>(
                    new CompleteEcommerceOAuthCommand(
                        new TenantId(payload.TenantId),
                        payload.PlatformCode,
                        payload.ExternalAccountId,
                        Code: string.Empty,
                        State: userId,
                        AdditionalParameters: additional),
                    ct).ConfigureAwait(false);
                return Results.Ok(new { connectionId = result.ConnectionId });
            }
            catch (Exception ex)
            {
                http.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("WooCommerceDashboardCallback")
                    .LogError(ex, "WooCommerce dashboard callback failed for tenant {Tenant}, store {Store}",
                        payload.TenantId, payload.ExternalAccountId);
                return Results.Problem(
                    title: "WooCommerce install completion failed.",
                    detail: $"{ex.GetType().Name}: {ex.Message}{(ex.InnerException is null ? "" : $" -> {ex.InnerException.GetType().Name}: {ex.InnerException.Message}")}",
                    statusCode: 500);
            }
        }).AllowAnonymous().WithName("DashboardCallbackWooCommerce");

        // Dev-only "fake-approve" landing for the InMemory WC connector. The synthetic install
        // URL points here; clicking through immediately POSTs synthesized creds back into the
        // anonymous callback so a developer can drive the same flow without spinning up WP.
        group.MapGet("/woocommerce/simulate", (
            string store,
            string user_id) =>
        {
            var html = $$"""
                <!doctype html><html><head><meta charset="utf-8"><title>Simulated WC approve</title></head>
                <body style="font-family:system-ui;padding:2rem;max-width:520px;margin:auto;">
                  <h2>Simulated WooCommerce approve</h2>
                  <p>Store: <code>{{store}}</code></p>
                  <p>This page mocks the wp-admin approval screen. Click below to POST synthetic
                  consumer keys to the orchestrator callback.</p>
                  <form method="POST" action="/v1/connections/dashboard-callback/woocommerce" enctype="application/json">
                    <input type="hidden" name="user_id" value="{{user_id}}" />
                  </form>
                  <button type="button" onclick="approve()">Approve (synthetic)</button>
                  <script>
                    async function approve() {
                      const body = {
                        key_id: 1,
                        user_id: {{System.Text.Json.JsonSerializer.Serialize(user_id)}},
                        consumer_key: 'ck_simulated_' + crypto.randomUUID().replaceAll('-',''),
                        consumer_secret: 'cs_simulated_' + crypto.randomUUID().replaceAll('-',''),
                        key_permissions: 'read_write'
                      };
                      const r = await fetch('/v1/connections/dashboard-callback/woocommerce', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify(body)
                      });
                      if (r.ok) {
                        document.body.innerHTML += '<p style="color:green">Connected. You can close this tab.</p>';
                        setTimeout(() => window.location.href = 'http://localhost:5173/connections?wc=connected', 800);
                      } else {
                        document.body.innerHTML += '<p style="color:red">Failed: ' + r.status + '</p>';
                      }
                    }
                  </script>
                </body></html>
                """;
            return Results.Content(html, "text/html");
        }).AllowAnonymous().WithName("DashboardCallbackWooCommerceSimulate");
    }

    private static bool TryGetString(JsonElement root, string name, out string? value)
    {
        value = null;
        if (!root.TryGetProperty(name, out var prop)) return false;
        value = prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Number => prop.GetRawText(),
            _ => null,
        };
        return value is not null;
    }

    private static string InstallReceivedHtml(string platform, Guid connectionId) =>
        $"""
        <html><body style="font-family:system-ui;padding:2rem;">
          <h2>{platform} connected.</h2>
          <p>Connection id: <code>{connectionId}</code></p>
          <p>You can close this tab and return to the dashboard.</p>
          <script>setTimeout(() => window.location.href = 'http://localhost:5173/connections?{platform.ToLowerInvariant()}=connected', 800);</script>
        </body></html>
        """;

    private static string InstallFailedHtml(string platform, string reason) =>
        $"""
        <html><body style="font-family:system-ui;padding:2rem;">
          <h2>{platform} install failed</h2>
          <pre>{System.Net.WebUtility.HtmlEncode(reason)}</pre>
        </body></html>
        """;
}
