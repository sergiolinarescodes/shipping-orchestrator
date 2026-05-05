using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Application.Tenancy;

public sealed record SuspendTenantCommand(TenantId TenantId, string Reason);

public static class SuspendTenantHandler
{
    public static async Task Handle(
        SuspendTenantCommand command,
        ITenantRepository tenantRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Reason);
        var tenant = await tenantRepository.FindAsync(command.TenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Tenant {command.TenantId} not found.");
        tenant.Suspend(clock.UtcNow, command.Reason);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
