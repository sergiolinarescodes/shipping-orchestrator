namespace ShippingOrchestrator.Modules.Abstractions.Carriers;

/// <summary>
/// Canonical contract every carrier integration implements. Carrier-native DTOs are mapped
/// inside the connector project's anti-corruption layer; the rest of the system never sees
/// PostNL/UPS/FedEx-specific shapes.
/// </summary>
public interface ICarrierConnector
{
    string CarrierCode { get; }

    Task<RateQuoteResult> QuoteAsync(RateQuoteRequest request, CancellationToken ct);

    Task<LabelCreationResult> CreateLabelAsync(LabelCreationRequest request, CancellationToken ct);

    Task<TrackingResult> TrackAsync(string trackingNumber, CancellationToken ct);

    Task<CancellationResult> CancelAsync(string trackingNumber, CancellationToken ct);
}
