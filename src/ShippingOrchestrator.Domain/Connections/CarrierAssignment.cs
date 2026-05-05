using ShippingOrchestrator.Domain.Abstractions;
using ShippingOrchestrator.Domain.Events;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Domain.ValueObjects;

namespace ShippingOrchestrator.Domain.Connections;

/// <summary>
/// Tells the routing engine: tenant T may use carrier C, with priority P, for shipments
/// going from any of <see cref="OriginCountries"/> to any of <see cref="DestinationCountries"/>.
/// A tenant can have many assignments — the routing engine ranks them at request time.
/// Country lists are stored as plain ISO-3166-1 alpha-2 strings (or "*" for wildcard) so
/// the EF Postgres provider can map them natively to <c>text[]</c> without a value
/// converter — strongly-typed projection happens at read time via <see cref="Origins"/>
/// / <see cref="Destinations"/>.
/// </summary>
public sealed class CarrierAssignment : AggregateRoot
{
    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string CarrierCode { get; private set; } = string.Empty;
    public int Priority { get; private set; }
    public bool IsActive { get; private set; }
    public string[] OriginCountries { get; private set; } = [];
    public string[] DestinationCountries { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IEnumerable<CountryCode> Origins => OriginCountries.Select(c => new CountryCode(c));
    public IEnumerable<CountryCode> Destinations => DestinationCountries.Select(c => new CountryCode(c));

    private CarrierAssignment() { }

    public static CarrierAssignment Create(
        TenantId tenantId,
        string carrierCode,
        int priority,
        IEnumerable<CountryCode> origins,
        IEnumerable<CountryCode> destinations,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(carrierCode);
        var assignment = new CarrierAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CarrierCode = carrierCode.ToLowerInvariant(),
            Priority = priority,
            IsActive = true,
            OriginCountries = origins.Select(c => c.Value).ToArray(),
            DestinationCountries = destinations.Select(c => c.Value).ToArray(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        assignment.Raise(new CarrierAssignmentCreated(
            assignment.Id, tenantId, assignment.CarrierCode, priority, now));
        return assignment;
    }

    public bool Covers(CountryCode origin, CountryCode destination)
    {
        var originOk = OriginCountries.Any(c => c == CountryCode.Wildcard || c == origin.Value);
        var destOk = DestinationCountries.Any(c => c == CountryCode.Wildcard || c == destination.Value);
        return IsActive && originOk && destOk;
    }

    public void Disable(DateTimeOffset now)
    {
        IsActive = false;
        UpdatedAt = now;
    }
}
