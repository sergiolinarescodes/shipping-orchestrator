using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Application.Connections;

/// <summary>
/// Hard-deletes a tenant's ecommerce connection. The product UX is "remove + re-install
/// fresh" rather than "soft-disable + reactivate" — every reconnect starts from zero, so
/// stale webhook secrets / dead delivery URLs / orphaned platform-side hooks can never
/// survive a disconnect. The platform-side cleanup (Shopify webhook delete, WooCommerce
/// webhook purge) happens on the NEXT install: PurgeOrchestratorWebhooksAsync deletes any
/// pre-existing Ship-Shipping-named hooks before registering fresh ones.
/// </summary>
public sealed record DisconnectEcommerceConnectionCommand(
    Guid ConnectionId,
    TenantId RequestingTenantId,
    string Reason);

public sealed record DisconnectEcommerceConnectionResult(Guid ConnectionId);

public static class DisconnectEcommerceConnectionHandler
{
    public static async Task<DisconnectEcommerceConnectionResult> Handle(
        DisconnectEcommerceConnectionCommand command,
        IEcommerceConnectionRepository connections,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var connection = await connections.FindAsync(command.ConnectionId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Connection {command.ConnectionId} not found.");

        // Tenant-isolation guard: a tenant must never be able to mutate another tenant's
        // connection by id. Throws so the endpoint can map to 403.
        if (connection.TenantId != command.RequestingTenantId)
            throw new UnauthorizedAccessException(
                $"Connection {command.ConnectionId} does not belong to tenant {command.RequestingTenantId}.");

        connections.Remove(connection);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new DisconnectEcommerceConnectionResult(connection.Id);
    }
}
