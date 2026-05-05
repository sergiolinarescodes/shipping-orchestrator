using Microsoft.Extensions.Options;
using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Email;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Application.Identity.Templates;
using ShippingOrchestrator.Domain.Identity;
using ShippingOrchestrator.Domain.Tenancy;
using Wolverine;

namespace ShippingOrchestrator.Application.Identity;

public sealed record InviteToTenantCommand(
    AccountId InviterAccountId,
    TenantId TenantId,
    string Email,
    MembershipRole Role);

public sealed record InviteToTenantResult(bool Success, Guid? InvitationId, string? FailureReason);

public static class InviteToTenantHandler
{
    public static async Task<InviteToTenantResult> Handle(
        InviteToTenantCommand command,
        IAccountRepository accounts,
        ITenantRepository tenants,
        ITenantMembershipRepository memberships,
        ITenantInvitationRepository invitations,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        IOptions<AuthOptions> options,
        IClock clock,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Email);

        var inviterMembership = await memberships
            .FindAsync(command.InviterAccountId, command.TenantId, cancellationToken)
            .ConfigureAwait(false);
        if (inviterMembership is null || inviterMembership.Role != MembershipRole.Owner)
            return new InviteToTenantResult(false, null, "not-owner");

        var inviter = await accounts.FindByIdAsync(command.InviterAccountId, cancellationToken).ConfigureAwait(false);
        if (inviter is null) return new InviteToTenantResult(false, null, "inviter-missing");

        var tenant = await tenants.FindAsync(command.TenantId, cancellationToken).ConfigureAwait(false);
        if (tenant is null) return new InviteToTenantResult(false, null, "tenant-missing");

        var inv = TenantInvitation.Create(
            command.TenantId, command.Email, command.InviterAccountId, command.Role, clock.UtcNow);
        await invitations.AddAsync(inv, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var requestLinkUrl = options.Value.DashboardBaseUrl.TrimEnd('/') + "/login";
        var message = InvitationEmail.Build(inv.Email, tenant.DisplayName, inviter.Email, requestLinkUrl);
        await bus.PublishAsync(new SendEmailCommand(message)).ConfigureAwait(false);

        return new InviteToTenantResult(true, inv.Id, null);
    }
}
