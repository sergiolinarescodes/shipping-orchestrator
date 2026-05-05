using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ShippingOrchestrator.Infrastructure.Telemetry;

public static class OrchestratorTelemetry
{
    public const string ActivitySourceName = "ShippingOrchestrator";
    public const string MeterName = "ShippingOrchestrator";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> ShipmentsBatched = Meter.CreateCounter<long>("shipments.batched");
    public static readonly Counter<long> CarrierCalls = Meter.CreateCounter<long>("carrier.calls");
    public static readonly Counter<long> CarrierErrors = Meter.CreateCounter<long>("carrier.errors");
    public static readonly Histogram<double> BatchLatencyMs = Meter.CreateHistogram<double>("batch.latency_ms");
    public static readonly Histogram<double> CarrierDurationMs = Meter.CreateHistogram<double>("carrier.duration_ms");
}
