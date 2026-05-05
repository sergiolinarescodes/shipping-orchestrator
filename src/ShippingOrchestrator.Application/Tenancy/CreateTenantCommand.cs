using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Application.Tenancy;

public sealed record CreateTenantCommand(
    string DisplayName,
    string? ContactEmail,
    TenantCarrierMode? CarrierMode = null,
    ToSAcceptance? ToSAcceptance = null,
    bool ActivateImmediately = false);

public sealed record CreateTenantResult(TenantId TenantId, TenantStatus Status);

public static class CreateTenantHandler
{
    public static async Task<CreateTenantResult> Handle(
        CreateTenantCommand command,
        ITenantRepository tenantRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var tenant = Tenant.Create(
            command.DisplayName,
            command.ContactEmail,
            clock.UtcNow,
            command.CarrierMode,
            command.ToSAcceptance,
            command.ActivateImmediately);
        await tenantRepository.AddAsync(tenant, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new CreateTenantResult(tenant.Id, tenant.Status);
    }
}
