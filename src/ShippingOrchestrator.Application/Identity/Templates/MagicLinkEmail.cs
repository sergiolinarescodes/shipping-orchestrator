using ShippingOrchestrator.Application.Common.Email;

namespace ShippingOrchestrator.Application.Identity.Templates;

public static class MagicLinkEmail
{
    public static EmailMessage Build(string toEmail, string link, DateTimeOffset expiresAt)
    {
        var minutes = (int)Math.Ceiling((expiresAt - DateTimeOffset.UtcNow).TotalMinutes);
        var safeMinutes = minutes < 1 ? 1 : minutes;
        var subject = "Your sign-in link";
        var text = $"""
            Sign in to Shipping Orchestrator by opening this link:

            {link}

            The link expires in {safeMinutes} minutes and works once.
            If you did not request this, ignore this email.
            """;
        var html = $"""
            <p>Sign in to <strong>Shipping Orchestrator</strong>:</p>
            <p><a href="{link}">{link}</a></p>
            <p>The link expires in {safeMinutes} minutes and works once. If you did not request this, ignore this email.</p>
            """;
        return new EmailMessage(toEmail, subject, text, html);
    }
}
