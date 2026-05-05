using ShippingOrchestrator.Application.Common.Email;

namespace ShippingOrchestrator.Application.Identity.Templates;

public static class InvitationEmail
{
    public static EmailMessage Build(string toEmail, string tenantName, string inviterEmail, string requestLinkUrl)
    {
        var subject = $"You've been invited to {tenantName}";
        var text = $"""
            {inviterEmail} invited you to join {tenantName} on Shipping Orchestrator.

            Sign in with your email at:
            {requestLinkUrl}

            On first sign-in we'll add you to the tenant automatically.
            """;
        var html = $"""
            <p><strong>{inviterEmail}</strong> invited you to join <strong>{tenantName}</strong> on Shipping Orchestrator.</p>
            <p>Sign in with your email at <a href="{requestLinkUrl}">{requestLinkUrl}</a>. On first sign-in we'll add you to the tenant automatically.</p>
            """;
        return new EmailMessage(toEmail, subject, text, html);
    }
}
