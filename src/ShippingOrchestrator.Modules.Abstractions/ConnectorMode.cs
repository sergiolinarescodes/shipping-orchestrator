namespace ShippingOrchestrator.Modules.Abstractions;

/// <summary>
/// Selects which implementation backs a connector code at runtime. Each connector module
/// reads its own <see cref="ConnectorModeOptions"/> (named per <c>ConnectorCode</c>) and
/// resolves the matching adapter. <see cref="InMemory"/> is rejected in Production by the
/// shared bootstrap guard in <see cref="CarrierConnectorModuleBase"/>.
/// </summary>
public enum ConnectorMode
{
    Real = 0,
    InMemory = 1,
}

/// <summary>Bound from <c>Connectors:{ConnectorCode}</c>. Stored as named options.</summary>
public sealed class ConnectorModeOptions
{
    public ConnectorMode Mode { get; set; } = ConnectorMode.Real;
}
