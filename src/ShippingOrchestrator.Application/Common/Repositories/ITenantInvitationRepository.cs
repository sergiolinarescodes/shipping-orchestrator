using ShippingOrchestrator.Domain.Identity;

namespace ShippingOrchestrator.Application.Common.Repositories;

public interface ITenantInvitationRepository
{
    Task AddAsync(TenantInvitation invitation, CancellationToken cancellationToken);
    Task<IReadOnlyList<TenantInvitation>> ListPendingByEmailAsync(string email, CancellationToken cancellationToken);
}
