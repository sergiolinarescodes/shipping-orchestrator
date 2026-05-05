using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Application.Onboarding.Verification;

namespace ShippingOrchestrator.Application.Connections;

public sealed record ReverifyConnectionCommand(Guid ConnectionId);

public sealed record ReverifyConnectionResult(string Status, string? RejectReason);

public static class ReverifyConnectionHandler
{
    public static async Task<ReverifyConnectionResult> Handle(
        ReverifyConnectionCommand command,
        IEcommerceConnectionRepository connections,
        IVerificationProvider verificationProvider,
        IActivationPolicy activationPolicy,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var connection = await connections.FindAsync(command.ConnectionId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Connection {command.ConnectionId} not found.");

        var verification = await verificationProvider.VerifyAsync(
                new VerificationRequest(connection.TenantId, connection.PlatformCode, connection.ExternalAccountId),
                cancellationToken)
            .ConfigureAwait(false);
        var decision = activationPolicy.Decide(verification);
        if (decision.Activate)
            connection.MarkVerified(clock.UtcNow);
        else if (decision.RejectReason is not null)
            connection.MarkRejected(decision.RejectReason, clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ReverifyConnectionResult(connection.Status.ToString(), connection.RejectionReason);
    }
}
