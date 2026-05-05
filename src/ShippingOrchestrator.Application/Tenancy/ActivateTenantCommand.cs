using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Application.Tenancy;

public sealed record ActivateTenantCommand(TenantId TenantId);

public static class ActivateTenantHandler
{
    public static async Task Handle(
        ActivateTenantCommand command,
        ITenantRepository tenantRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.FindAsync(command.TenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Tenant {command.TenantId} not found.");
        tenant.Activate(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
