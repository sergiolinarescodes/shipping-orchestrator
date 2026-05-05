namespace ShippingOrchestrator.CarrierConnectors.PostNL;

/// <summary>
/// Configuration for the in-process simulator. Bound from
/// <c>Connectors:PostNL:InMemory</c>. Only consumed by
/// <see cref="PostNlInMemoryCarrierConnector"/>; the production options
/// (<see cref="PostNlRealOptions"/>) never see these knobs.
/// </summary>
public sealed class PostNlInMemoryOptions
{
    /// <summary>Lower bound of simulated label-creation latency. Defaults to 200ms.</summary>
    public int MinLatencyMs { get; set; } = 200;

    /// <summary>Upper bound of simulated label-creation latency. Defaults to 1500ms.</summary>
    public int MaxLatencyMs { get; set; } = 1500;

    /// <summary>0.0–1.0 probability the simulator returns a failure result. Defaults to 0.</summary>
    public double FailureProbability { get; set; }

    /// <summary>Base URL the simulator prints on returned label URIs.</summary>
    public string LabelStorageBaseUri { get; set; } = "https://mock.postnl.local/labels";
}
