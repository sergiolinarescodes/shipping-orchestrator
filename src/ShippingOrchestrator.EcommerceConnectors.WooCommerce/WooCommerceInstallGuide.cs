using ShippingOrchestrator.Modules.Abstractions;

namespace ShippingOrchestrator.EcommerceConnectors.WooCommerce;

/// <summary>
/// Tenant-facing install checklist for WooCommerce. The user is redirected to their wp-admin
/// to approve a REST API key — same flow locally and in production, the only difference is
/// the InMemory simulation page substitutes for wp-admin.
/// </summary>
internal sealed class WooCommerceInstallGuide(ConnectorMode mode) : IInstallGuideProvider
{
    private const string SiteUrlKey = "siteUrl";

    public InstallGuide GetGuide(InstallGuideContext context) => mode switch
    {
        ConnectorMode.Real => new InstallGuide(
            Title: "Connect WooCommerce",
            Steps:
            [
                "Enter your store URL below.",
                "Approve the API key prompt in your WordPress admin.",
                "You'll be redirected back here automatically.",
            ],
            Inputs:
            [
                new InstallInputField(
                    Key: SiteUrlKey,
                    Label: "Store URL",
                    Placeholder: "https://your-store.com",
                    Required: true,
                    HelpText: "The full URL — must include https://"),
            ],
            HelpUrl: "https://woocommerce.com/document/woocommerce-rest-api/"),

        ConnectorMode.InMemory => new InstallGuide(
            Title: "Connect WooCommerce (dev)",
            Steps:
            [
                "Enter any store URL — local mode auto-approves.",
                "Click Continue to simulate the install.",
            ],
            Inputs:
            [
                new InstallInputField(
                    Key: SiteUrlKey,
                    Label: "Store URL",
                    Placeholder: "https://demo.local",
                    Required: true),
            ]),

        _ => throw new InvalidOperationException($"Unknown WooCommerce mode '{mode}'."),
    };
}
