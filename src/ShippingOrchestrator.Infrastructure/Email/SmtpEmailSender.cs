using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShippingOrchestrator.Application.Common.Email;

namespace ShippingOrchestrator.Infrastructure.Email;

/// <summary>
/// SMTP-backed <see cref="IEmailSender"/>. Uses <c>System.Net.Mail.SmtpClient</c> for
/// dependency-free transactional email — fine for Mailpit in dev and AWS SES SMTP in prod.
/// If we ever need server-side templates, attachments, or SES API features (suppression
/// list, configuration sets), swap in the AWS SDK or MailKit; the interface insulates callers.
/// </summary>
internal sealed class SmtpEmailSender : IEmailSender
{
    private readonly IOptions<SmtpOptions> _options;
    private readonly ILogger<SmtpEmailSender> _log;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> log)
    {
        _options = options;
        _log = log;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var opt = _options.Value;
        using var smtp = new SmtpClient(opt.Host, opt.Port)
        {
            EnableSsl = opt.UseStartTls,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };
        if (!string.IsNullOrEmpty(opt.Username))
            smtp.Credentials = new NetworkCredential(opt.Username, opt.Password);

        using var mail = new MailMessage
        {
            From = new MailAddress(opt.FromAddress, opt.FromDisplayName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true,
        };
        mail.To.Add(message.To);
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            message.TextBody, null, "text/plain"));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            message.HtmlBody, null, "text/html"));

        try
        {
            await smtp.SendMailAsync(mail, cancellationToken).ConfigureAwait(false);
            _log.LogInformation("Sent {Subject} to {To} via {Host}:{Port}",
                message.Subject, message.To, opt.Host, opt.Port);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to send email to {To} via {Host}:{Port}",
                message.To, opt.Host, opt.Port);
            throw;
        }
    }
}
