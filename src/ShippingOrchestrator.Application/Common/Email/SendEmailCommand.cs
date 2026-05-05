namespace ShippingOrchestrator.Application.Common.Email;

/// <summary>
/// Wolverine message dispatched from in-transaction handlers when an email needs to be
/// sent. Routed via the durable outbox so the email is only attempted after the producing
/// transaction commits — no premature emails on rollback.
/// </summary>
public sealed record SendEmailCommand(EmailMessage Message);

public static class SendEmailHandler
{
    public static Task Handle(
        SendEmailCommand command,
        IEmailSender sender,
        CancellationToken cancellationToken) =>
        sender.SendAsync(command.Message, cancellationToken);
}
