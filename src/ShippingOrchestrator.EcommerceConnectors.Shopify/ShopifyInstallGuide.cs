using ShippingOrchestrator.Modules.Abstractions;

namespace ShippingOrchestrator.EcommerceConnectors.Shopify;

/// <summary>
/// Tenant-facing install checklist surfaced by the customer SPA when a tenant clicks Connect
/// on the Shopify card. Copy is intentionally minimal — the SPA renders these as short
/// numbered bullets, not a manual. Real-mode and in-memory-mode produce different copy
/// because the in-memory adapter auto-approves locally and the user does not visit Shopify.
/// </summary>
internal sealed class ShopifyInstallGuide(ConnectorMode mode) : IInstallGuideProvider
{
    private const string ShopDomainKey = "shopDomain";

    public InstallGuide GetGuide(InstallGuideContext context) => mode switch
    {
        ConnectorMode.Real => new InstallGuide(
            Title: "Connect Shopify",
            Steps:
            [
                "Install the Ship Shipping app from the Shopify App Store.",
                "Approve the requested scopes when Shopify prompts you.",
                "Enter your *.myshopify.com domain below and click Continue.",
            ],
            Inputs:
            [
                new InstallInputField(
                    Key: ShopDomainKey,
                    Label: "Shop domain",
                    Placeholder: "your-store.myshopify.com",
                    Required: true,
                    HelpText: "Find this in Shopify admin → Settings → Domains."),
            ],
            HelpUrl: "https://help.shopify.com/manual/apps/installing-apps"),

        ConnectorMode.InMemory => new InstallGuide(
            Title: "Connect Shopify (dev)",
            Steps:
            [
                "Enter any *.myshopify.com domain — local mode auto-approves.",
                "Click Continue to simulate the install.",
            ],
            Inputs:
            [
                new InstallInputField(
                    Key: ShopDomainKey,
                    Label: "Shop domain",
                    Placeholder: "demo.myshopify.com",
                    Required: true),
            ]),

        _ => throw new InvalidOperationException($"Unknown Shopify mode '{mode}'."),
    };
}
