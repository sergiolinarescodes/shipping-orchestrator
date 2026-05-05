using System.Text;
using ShippingOrchestrator.Application.Common.Encryption;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Connections;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Modules.Abstractions;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;
using Wolverine;
using DomainReason = ShippingOrchestrator.Domain.Ingestion.IngestionReasonCode;

namespace ShippingOrchestrator.Application.Ingestion;

/// <summary>
/// Tenant-triggered (or staff-triggered) re-pull of a single failed order from the ecommerce
/// platform. Used when the tenant has fixed the underlying issue in their store (added a
/// product weight, set a destination country, …) and wants the orchestrator to re-evaluate
/// the order without having to manually re-save it to fire a fresh <c>orders/updated</c>
/// webhook.
///
/// Flow:
/// <list type="number">
/// <item>Look up the failure (tenant-scoped — caller can't recheck another tenant's row).</item>
/// <item>Resolve the active <see cref="EcommerceConnection"/> for <c>(tenant, connectorCode)</c>.
///   Must be exactly one — multiple stores per platform per tenant is rare; v1 returns
///   <see cref="RecheckOutcome.AmbiguousConnection"/> in that case rather than guessing.</item>
/// <item>Cast the connector to <see cref="IEcommerceOrderFetcher"/>. Connectors opt in;
///   fallback returns <see cref="RecheckOutcome.NotSupported"/>.</item>
/// <item>Decrypt credentials → call fetcher → run translator → on success dispatch
///   <see cref="IngestEcommerceOrderCommand"/> (auto-resolve already wired there);
///   on translation failure dispatch <see cref="RecordIngestionFailureCommand"/> so the
///   same row gets updated via <c>Reoccur</c> with the latest reason.</item>
/// </list>
///
/// Failures with no <see cref="Domain.Ingestion.IngestionFailure.ExternalOrderId"/>
/// (parse-error rows) cannot be rechecked — there's nothing to address against. The handler
/// returns <see cref="RecheckOutcome.NotRecheckable"/>.
/// </summary>
public sealed record RecheckIngestionFailureCommand(
    Guid FailureId,
    TenantId TenantId);

public enum RecheckOutcome
{
    /// <summary>Translator now succeeds. Failure auto-resolved; pending order created (or already present).</summary>
    Resolved,
    /// <summary>Translator still throws. Failure row updated with the latest reason via Reoccur.</summary>
    StillFailing,
    /// <summary>No failure with this id under this tenant.</summary>
    NotFound,
    /// <summary>Failure has no external order id — nothing to look up on the platform.</summary>
    NotRecheckable,
    /// <summary>Tenant has no active connection for this connector. Reinstall required.</summary>
    NoConnection,
    /// <summary>Tenant has multiple active connections for the same platform — can't disambiguate without storing externalAccountId on the failure row. v1 limitation.</summary>
    AmbiguousConnection,
    /// <summary>Connector doesn't implement <see cref="IEcommerceOrderFetcher"/>.</summary>
    NotSupported,
    /// <summary>Reaching the platform failed (404, 5xx, network).</summary>
    FetchFailed,
}

public sealed record RecheckIngestionFailureResult(
    RecheckOutcome Outcome,
    string? Detail = null,
    Guid? PendingOrderId = null);

public static class RecheckIngestionFailureHandler
{
    public static async Task<RecheckIngestionFailureResult> Handle(
        RecheckIngestionFailureCommand command,
        IIngestionFailureRepository failures,
        IEcommerceConnectionRepository connections,
        IEcommerceOrderTranslatorRegistry translators,
        IEnvelopeEncryptor encryptor,
        IRawBodyRedactor redactor,
        ConnectorRegistry connectorRegistry,
        IServiceProvider services,
        IMessageBus bus,
        IIngestionDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var failure = await failures.FindAsync(command.FailureId, cancellationToken).ConfigureAwait(false);
        if (failure is null || failure.TenantId != command.TenantId)
            return new RecheckIngestionFailureResult(RecheckOutcome.NotFound);

        if (string.IsNullOrWhiteSpace(failure.ExternalOrderId))
            return new RecheckIngestionFailureResult(
                RecheckOutcome.NotRecheckable,
                "This failure has no external order id (parse error). Re-send the corrected order from your store.");

        var allConnections = await connections.ListForTenantAsync(command.TenantId, cancellationToken).ConfigureAwait(false);
        var matching = allConnections
            .Where(c => string.Equals(c.PlatformCode, failure.ConnectorCode, StringComparison.OrdinalIgnoreCase)
                        && c.Status == EcommerceConnectionStatus.Active)
            .ToList();
        if (matching.Count == 0)
            return new RecheckIngestionFailureResult(
                RecheckOutcome.NoConnection,
                $"No active {failure.ConnectorCode} connection for this tenant. Reinstall the store and try again.");
        if (matching.Count > 1)
            return new RecheckIngestionFailureResult(
                RecheckOutcome.AmbiguousConnection,
                $"Multiple active {failure.ConnectorCode} connections; cannot disambiguate. Recheck is single-store today.");
        var connection = matching[0];

        if (!connectorRegistry.TryGet(failure.ConnectorCode, out var registration) || registration is null)
            return new RecheckIngestionFailureResult(RecheckOutcome.NotSupported,
                $"Connector '{failure.ConnectorCode}' is not registered on this host.");
        var connector = registration.ConnectorFactory(services);
        if (connector is not IEcommerceOrderFetcher fetcher)
            return new RecheckIngestionFailureResult(RecheckOutcome.NotSupported,
                $"Connector '{failure.ConnectorCode}' does not support on-demand order fetching.");

        var creds = await encryptor.DecryptAsync(connection.CredentialsCipher, cancellationToken).ConfigureAwait(false);
        var fetchResult = await fetcher.FetchRawOrderAsync(
            new OrderFetchRequest(connection.ExternalAccountId, failure.ExternalOrderId, creds),
            cancellationToken).ConfigureAwait(false);
        if (!fetchResult.Success || fetchResult.RawBody is null)
            return new RecheckIngestionFailureResult(RecheckOutcome.FetchFailed, fetchResult.FailureReason);

        if (!translators.TryResolve(failure.ConnectorCode, out var translator) || translator is null)
            return new RecheckIngestionFailureResult(RecheckOutcome.NotSupported,
                $"No translator registered for '{failure.ConnectorCode}'.");

        try
        {
            var payload = await translator.TranslateAsync(
                command.TenantId,
                connection.ExternalAccountId,
                fetchResult.RawBody,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                cancellationToken).ConfigureAwait(false);

            // Same enrich-then-validate sequence the webhook path uses. Recheck exists precisely
            // for the case where a tenant fixed a product (e.g. set the weight) and wants the
            // pending row re-evaluated; running enrichment here is the whole point.
            if (connector is IEcommerceOrderEnricher enricher)
            {
                payload = await enricher.EnrichAsync(payload, creds, cancellationToken).ConfigureAwait(false);
            }
            if (payload.TotalWeight.Grams <= 0)
            {
                throw new IngestionTranslationException(
                    IngestionReasonCode.ZeroWeight,
                    failure.ConnectorCode,
                    payload.ExternalOrderId,
                    "Set a weight on the product (Products → Edit → Shipping tab → Weight). " +
                    "Then click Recheck again to re-pull the order with the updated weight.",
                    $"{failure.ConnectorCode} order has zero total weight across line items after enrichment.");
            }

            var result = await dispatcher.DispatchAsync(payload, cancellationToken).ConfigureAwait(false);
            // Auto-resolve hook in IngestEcommerceOrderHandler will have flipped the failure
            // to Resolved in the same EF transaction. No second write needed here.
            return new RecheckIngestionFailureResult(RecheckOutcome.Resolved, PendingOrderId: result.PendingOrderId);
        }
        catch (IngestionTranslationException ex)
        {
            // Same logic the webhook endpoint uses — record the failure so the existing row
            // gets updated via Reoccur (with the latest reason if it changed).
            var rawBytes = Encoding.UTF8.GetBytes(fetchResult.RawBody);
            await bus.InvokeAsync<RecordIngestionFailureResult>(
                new RecordIngestionFailureCommand(
                    command.TenantId,
                    failure.ConnectorCode,
                    ex.ExternalOrderId,
                    (DomainReason)(int)ex.Code,
                    ex.Message,
                    ex.TenantHint,
                    redactor.Redact(fetchResult.RawBody),
                    redactor.Hash(rawBytes),
                    ex.Context),
                cancellationToken).ConfigureAwait(false);
            return new RecheckIngestionFailureResult(RecheckOutcome.StillFailing, ex.TenantHint);
        }
    }
}
