using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;

namespace ShippingOrchestrator.Application.Identity;

public sealed record SignOutCommand(Guid SessionId);

public sealed record SignOutResult;

public static class SignOutHandler
{
    public static async Task<SignOutResult> Handle(
        SignOutCommand command,
        IAuthSessionRepository sessions,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var session = await sessions.FindByIdAsync(command.SessionId, cancellationToken).ConfigureAwait(false);
        if (session is null) return new SignOutResult();
        session.Revoke(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new SignOutResult();
    }
}
