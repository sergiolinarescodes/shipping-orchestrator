namespace ShippingOrchestrator.Modules.Abstractions;

/// <summary>
/// Singleton runtime registry of every loaded connector. Resolves a concrete
/// <c>ICarrierConnector</c> or <c>IEcommerceConnector</c> by code at request time.
/// Mirrors the <c>PipelineRegistry</c> pattern from the reference repo
/// (<c>mec-finops-crucible</c>) but partitioned by <see cref="ConnectorKind"/>.
/// </summary>
public sealed class ConnectorRegistry
{
    private readonly Dictionary<string, ConnectorRegistration> _registrations =
        new(StringComparer.OrdinalIgnoreCase);

    public void Register(ConnectorRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        _registrations[registration.ConnectorCode] = registration;
    }

    public ConnectorRegistration Get(string connectorCode)
    {
        if (_registrations.TryGetValue(connectorCode, out var r)) return r;
        throw new KeyNotFoundException($"Connector '{connectorCode}' is not registered.");
    }

    public bool TryGet(string connectorCode, out ConnectorRegistration? registration) =>
        _registrations.TryGetValue(connectorCode, out registration);

    public IReadOnlyList<ConnectorRegistration> All() => [.. _registrations.Values];

    public IEnumerable<ConnectorRegistration> Of(ConnectorKind kind) =>
        _registrations.Values.Where(r => r.Kind == kind);

    /// <summary>
    /// Resolves the connector's tenant-facing install guide if it ships one. Used by
    /// PublicApi's <c>GET /v1/dashboard/connections/{platform}/install-guide</c> endpoint.
    /// </summary>
    public IInstallGuideProvider? ResolveInstallGuide(string connectorCode, IServiceProvider services)
    {
        if (!_registrations.TryGetValue(connectorCode, out var registration)) return null;
        if (registration.InstallGuideFactory is null) return null;
        try { return registration.InstallGuideFactory(services); }
        catch (NotImplementedException) { return null; }
    }
}

public sealed record ConnectorRegistration
{
    public required string ConnectorCode { get; init; }
    public required ConnectorKind Kind { get; init; }
    public required string DisplayName { get; init; }

    /// <summary>Resolves the connector implementation per scope. Boxed because this record is non-generic.</summary>
    public required Func<IServiceProvider, object> ConnectorFactory { get; init; }

    /// <summary>
    /// Resolves the customer-facing install guide for this connector, or null if the connector
    /// does not yet expose one. Always supply for ecommerce connectors that the tenant can
    /// install themselves from the dashboard.
    /// </summary>
    public Func<IServiceProvider, IInstallGuideProvider>? InstallGuideFactory { get; init; }

    /// <summary>Resolved <see cref="ConnectorMode"/> at registration time. Surfaced for endpoints that
    /// need to branch on local vs prod behaviour without re-reading options.</summary>
    public ConnectorMode Mode { get; init; }

    /// <summary>Optional metadata the dashboards expose (capabilities, country list, etc.).</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
