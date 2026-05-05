namespace ShippingOrchestrator.Domain.Ingestion;

/// <summary>
/// Closed taxonomy of webhook-translation failure reasons. Mirrored in
/// <c>ShippingOrchestrator.Modules.Abstractions.Ecommerce.IngestionReasonCode</c> so
/// connectors can throw a typed exception without taking a Domain dependency. Member names
/// and ordinal values must stay in lockstep — enforced by an architecture parity test.
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
