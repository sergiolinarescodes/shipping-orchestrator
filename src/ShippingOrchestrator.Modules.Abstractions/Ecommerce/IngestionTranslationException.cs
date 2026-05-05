using System.Collections.ObjectModel;

namespace ShippingOrchestrator.Modules.Abstractions.Ecommerce;

/// <summary>
/// Raised by an <see cref="IEcommerceOrderTranslator"/> when an inbound webhook payload is
/// well-formed enough to identify the order but not translatable into a clean
/// <c>EcommerceOrderPayload</c>. The webhook endpoint catches this, persists an
/// <c>IngestionException</c> aggregate, and 200-acks so the platform doesn't retry.
///
/// Connectors should always prefer this over generic <see cref="InvalidOperationException"/>
/// — the typed reason code drives the customer-facing "Needs attention" hint and the ops-side
/// usage analytics. Anything else thrown by a translator is treated as
/// <see cref="IngestionReasonCode.ParseError"/>.
/// </summary>
public sealed class IngestionTranslationException : Exception
{
    public IngestionReasonCode Code { get; }
    public string ConnectorCode { get; }
    public string? ExternalOrderId { get; }
    public string TenantHint { get; }
    public IReadOnlyDictionary<string, string> Context { get; }

    public IngestionTranslationException(
        IngestionReasonCode code,
        string connectorCode,
        string? externalOrderId,
        string tenantHint,
        string message,
        IReadOnlyDictionary<string, string>? context = null,
        Exception? inner = null)
        : base(message, inner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code;
        ConnectorCode = connectorCode;
        ExternalOrderId = externalOrderId;
        TenantHint = tenantHint ?? string.Empty;
        Context = context ?? ReadOnlyDictionary<string, string>.Empty;
    }

    /// <summary>
    /// Convenience for the most common case: the body is missing a required field. Saves
    /// connector authors from constructing the long-form exception manually.
    /// </summary>
    public static IngestionTranslationException Missing(
        string connectorCode,
        string? externalOrderId,
        string what,
        string tenantHint) =>
        new(IngestionReasonCode.MissingShippingAddress,
            connectorCode,
            externalOrderId,
            tenantHint,
            $"{connectorCode} order is missing {what}.");
}
