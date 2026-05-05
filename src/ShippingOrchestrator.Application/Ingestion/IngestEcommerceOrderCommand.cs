using System.Text.Json;
using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Ingestion;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;

namespace ShippingOrchestrator.Application.Ingestion;

/// <summary>
/// Single ingestion entry point for any normalized ecommerce order. Real Shopify webhooks and
/// the admin simulator both land here. The handler does not create a shipment batch directly:
/// it persists the order as <see cref="PendingEcommerceOrder"/> in an inbox-style table, and
/// the customer dashboard's "bundle pending orders" action is what turns several pending rows
/// into a single shipment batch via <c>BundlePendingOrdersCommand</c>. Idempotency is enforced
/// by the unique <c>(tenantId, platform, externalOrderId)</c> index — a Shopify retry of the
/// same order returns the existing pending row.
///
/// <para><see cref="PreallocatedPendingId"/> is the id the handler assigns to the new pending
/// row when none exists. Publishers (webhook intake, simulator, recheck) generate it before
/// publishing so they can return it synchronously — required for fire-and-forget routing
/// through SQS, where the handler runs on a different process and the publisher cannot await
/// a server-generated id.</para>
/// </summary>
public sealed record IngestEcommerceOrderCommand(EcommerceOrderPayload Payload, Guid PreallocatedPendingId);

public sealed record IngestEcommerceOrderResult(Guid PendingOrderId, bool AlreadyPending);

/// <summary>
/// Synchronous acknowledgement returned by <see cref="IIngestionDispatcher.DispatchAsync"/>.
/// Whether the publisher actually placed a message on SQS (<see cref="AlreadyPending"/> false)
/// or short-circuited because the unique index would have rejected the row anyway
/// (<see cref="AlreadyPending"/> true), the caller can return this immediately to the webhook
/// caller without waiting for the receiving Worker to commit.
/// </summary>
public sealed record IngestionAck(Guid PendingOrderId, bool AlreadyPending);

public static class IngestEcommerceOrderHandler
{
    public static async Task<IngestEcommerceOrderResult> Handle(
        IngestEcommerceOrderCommand command,
        IPendingEcommerceOrderRepository pendingRepo,
        IIngestionFailureRepository failureRepo,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var payload = command.Payload;
        var connectorKey = payload.ConnectorCode.ToLowerInvariant();

        // Auto-resume: a successful translation against this (tenant, connector, externalOrderId)
        // means whatever blocked the previous attempt (missing address, zero weight, etc.) has
        // been corrected upstream. Resolve any open failure in the same EF transaction so the
        // tenant's "Needs attention" row clears and ops sees the count drop. Wolverine's
        // AutoApplyTransactions enrolls the SaveChangesAsync below.
        var openFailure = await failureRepo
            .FindOpenByExternalOrderIdAsync(payload.TenantId, connectorKey, payload.ExternalOrderId, cancellationToken)
            .ConfigureAwait(false);
        openFailure?.Resolve("auto:order-translated-successfully", clock.UtcNow);

        var existing = await pendingRepo
            .FindByExternalIdAsync(payload.TenantId, payload.ConnectorCode, payload.ExternalOrderId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            if (openFailure is not null)
                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new IngestEcommerceOrderResult(existing.Id, AlreadyPending: true);
        }

        var json = JsonSerializer.Serialize(payload);
        var order = new PendingEcommerceOrder(
            id: command.PreallocatedPendingId == Guid.Empty ? Guid.NewGuid() : command.PreallocatedPendingId,
            tenantId: payload.TenantId,
            platformCode: payload.ConnectorCode,
            externalOrderId: payload.ExternalOrderId,
            payloadJson: json,
            ingestedAt: clock.UtcNow);
        await pendingRepo.AddAsync(order, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new IngestEcommerceOrderResult(order.Id, AlreadyPending: false);
    }
}
