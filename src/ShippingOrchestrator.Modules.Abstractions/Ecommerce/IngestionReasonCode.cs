namespace ShippingOrchestrator.Modules.Abstractions.Ecommerce;

/// <summary>
/// Mirror of <c>ShippingOrchestrator.Domain.Ingestion.IngestionReasonCode</c>. Connectors
/// throw <see cref="IngestionTranslationException"/> with one of these codes; the webhook
/// endpoint integer-casts to the Domain enum at the seam. Member names + ordinal values
/// must stay in lockstep with the Domain copy — enforced by an architecture parity test.
/// </summary>
public enum IngestionReasonCode
{
    Unknown = 0,
    MissingShippingAddress = 1,
    UnknownCountry = 2,
    ZeroWeight = 3,
    InvalidPostalCode = 4,
    UnsupportedCurrency = 5,
    ParseError = 6,
}
