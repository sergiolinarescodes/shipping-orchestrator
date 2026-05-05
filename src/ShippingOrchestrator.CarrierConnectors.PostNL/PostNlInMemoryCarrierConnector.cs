using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShippingOrchestrator.Domain.ValueObjects;
using ShippingOrchestrator.Modules.Abstractions.Carriers;

namespace ShippingOrchestrator.CarrierConnectors.PostNL;

/// <summary>
/// In-process simulator for PostNL — used in local dev (docker compose) and the E2E suite.
/// The canonical contract <see cref="ICarrierConnector"/> stays exactly the shape every
/// other carrier (real or simulated) implements, so swapping in the production PostNL
/// client later is a pure adapter swap behind the same registry factory.
/// Selection is controlled by <c>Connectors:PostNL:Mode</c>; this implementation is only
/// wired when <see cref="ConnectorMode.InMemory"/> is configured, and the connector
/// module rejects that mode in Production at startup via the shared base class.
/// </summary>
public sealed class PostNlInMemoryCarrierConnector(
    IOptions<PostNlInMemoryOptions> options,
    ILogger<PostNlInMemoryCarrierConnector> log) : ICarrierConnector
{
    private readonly PostNlInMemoryOptions _options = options.Value;

    public string CarrierCode => "postnl";

    public Task<RateQuoteResult> QuoteAsync(RateQuoteRequest request, CancellationToken ct)
    {
        var price = new Money(7.95m + 0.10m * request.Parcel.Weight.Kilograms, "EUR");
        var option = new RateQuoteOption(
            CarrierServiceCode: "PNL_3085",
            ServiceLevel: ServiceLevel.Standard,
            Price: price,
            EstimatedTransitTime: TimeSpan.FromDays(1));
        return Task.FromResult(new RateQuoteResult(true, [option]));
    }

    public async Task<LabelCreationResult> CreateLabelAsync(LabelCreationRequest request, CancellationToken ct)
    {
        await SimulateCarrierLatencyAsync(ct).ConfigureAwait(false);

        if (Random.Shared.NextDouble() < _options.FailureProbability)
        {
            log.LogInformation("PostNL simulator: forced failure for shipment {Shipment}", request.ShipmentId);
            return new LabelCreationResult(false, null, null, null, "PostNL simulator forced failure");
        }

        var trackingNumber = $"3SAB{Guid.NewGuid():N}"[..13].ToUpperInvariant();
        var labelUri = $"{_options.LabelStorageBaseUri.TrimEnd('/')}/{request.ShipmentId:N}.pdf";
        var price = new Money(7.95m + 0.10m * request.Parcel.Weight.Kilograms, "EUR");
        return new LabelCreationResult(true, trackingNumber, labelUri, price);
    }

    public Task<TrackingResult> TrackAsync(string trackingNumber, CancellationToken ct)
    {
        var events = new[]
        {
            new TrackingEvent(DateTimeOffset.UtcNow.AddHours(-2), "Accepted", "Parcel accepted at depot", "Hoofddorp"),
            new TrackingEvent(DateTimeOffset.UtcNow.AddMinutes(-15), "InTransit", "On route", "Amsterdam"),
        };
        return Task.FromResult(new TrackingResult(true, "InTransit", events));
    }

    public Task<CancellationResult> CancelAsync(string trackingNumber, CancellationToken ct) =>
        Task.FromResult(new CancellationResult(true));

    private async Task SimulateCarrierLatencyAsync(CancellationToken ct)
    {
        var delayMs = Random.Shared.Next(_options.MinLatencyMs, Math.Max(_options.MinLatencyMs + 1, _options.MaxLatencyMs));
        await Task.Delay(delayMs, ct).ConfigureAwait(false);
    }
}
