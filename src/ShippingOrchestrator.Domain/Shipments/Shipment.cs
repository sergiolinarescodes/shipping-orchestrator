using ShippingOrchestrator.Domain.Abstractions;
using ShippingOrchestrator.Domain.Events;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Domain.ValueObjects;

namespace ShippingOrchestrator.Domain.Shipments;

public sealed class Shipment : AggregateRoot
{
    private readonly List<ShipmentTrackingEvent> _trackingEvents = [];

    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public Guid? BatchId { get; private set; }
    public ShipmentStatus Status { get; private set; }
    public string? CarrierCode { get; private set; }
    public string? TrackingNumber { get; private set; }
    public string? LabelUri { get; private set; }
    public string? FailureReason { get; private set; }

    public Address From { get; private set; } = default!;
    public Address To { get; private set; } = default!;
    public Parcel Parcel { get; private set; } = default!;
    public ServiceLevel? PreferredService { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<ShipmentTrackingEvent> TrackingEvents => _trackingEvents;

    private Shipment() { }

    public static Shipment Create(
        TenantId tenantId,
        Guid? batchId,
        Address from,
        Address to,
        Parcel parcel,
        ServiceLevel? preferredService,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(parcel);
        var shipment = new Shipment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BatchId = batchId,
            Status = ShipmentStatus.Created,
            From = from,
            To = to,
            Parcel = parcel,
            PreferredService = preferredService,
            CreatedAt = now,
            UpdatedAt = now,
        };
        shipment.Raise(new ShipmentCreated(shipment.Id, tenantId, batchId, now));
        return shipment;
    }

    public void SelectCarrier(string carrierCode, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(carrierCode);
        EnsureStatus(ShipmentStatus.Created);
        CarrierCode = carrierCode.ToLowerInvariant();
        Status = ShipmentStatus.CarrierSelected;
        UpdatedAt = now;
        Raise(new ShipmentCarrierSelected(Id, TenantId, CarrierCode, now));
    }

    public void MarkLabelRequested(DateTimeOffset now)
    {
        EnsureStatus(ShipmentStatus.CarrierSelected);
        Status = ShipmentStatus.LabelRequested;
        UpdatedAt = now;
    }

    public void RecordLabel(string trackingNumber, string labelUri, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(labelUri);
        if (Status is not (ShipmentStatus.LabelRequested or ShipmentStatus.CarrierSelected))
            throw new InvalidOperationException($"Cannot record label from status {Status}.");
        TrackingNumber = trackingNumber;
        LabelUri = labelUri;
        Status = ShipmentStatus.Labeled;
        UpdatedAt = now;
        Raise(new ShipmentLabeled(Id, TenantId, CarrierCode!, trackingNumber, labelUri, now));
    }

    public void Fail(string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        FailureReason = reason;
        Status = ShipmentStatus.Failed;
        UpdatedAt = now;
        Raise(new ShipmentFailed(Id, TenantId, reason, now));
    }

    public void Cancel(string reason, DateTimeOffset now)
    {
        if (Status is ShipmentStatus.Delivered)
            throw new InvalidOperationException("Cannot cancel a delivered shipment.");
        Status = ShipmentStatus.Cancelled;
        FailureReason = reason;
        UpdatedAt = now;
        Raise(new ShipmentCancelled(Id, TenantId, reason, now));
    }

    /// <summary>
    /// Appends carrier-reported tracking events that haven't been seen yet (matched by
    /// <c>EventCode</c> + <c>OccurredAt</c>). Idempotent — re-applying the same poll result
    /// is a no-op. Promotes the shipment status to <see cref="ShipmentStatus.InTransit"/> or
    /// <see cref="ShipmentStatus.Delivered"/> when the carrier signals a terminal state.
    /// </summary>
    public bool AppendTrackingEvents(IEnumerable<ShipmentTrackingUpdate> incoming, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        var added = new List<ShipmentTrackingEvent>();
        var nextSequence = _trackingEvents.Count == 0 ? 0 : _trackingEvents.Max(e => e.Sequence) + 1;
        foreach (var e in incoming)
        {
            if (_trackingEvents.Any(existing => existing.EventCode == e.EventCode && existing.OccurredAt == e.OccurredAt))
                continue;
            var entity = ShipmentTrackingEvent.Create(Id, nextSequence++, e.EventCode, e.Description, e.Location, e.OccurredAt);
            _trackingEvents.Add(entity);
            added.Add(entity);
        }
        if (added.Count == 0) return false;

        var latestStatus = added
            .OrderByDescending(e => e.OccurredAt)
            .Select(e => e.EventCode)
            .FirstOrDefault();

        var promoted = PromoteStatus(latestStatus);
        if (promoted) UpdatedAt = now;
        Raise(new ShipmentTrackingUpdated(
            Id,
            TenantId,
            added.Select(e => new ShipmentTrackingUpdate(e.EventCode, e.Description, e.Location, e.OccurredAt)).ToArray(),
            latestStatus,
            now));
        return true;
    }

    private bool PromoteStatus(string? carrierStatus)
    {
        if (string.IsNullOrWhiteSpace(carrierStatus)) return false;
        return carrierStatus switch
        {
            "Delivered" when Status is ShipmentStatus.Labeled or ShipmentStatus.InTransit => SetStatus(ShipmentStatus.Delivered),
            "InTransit" or "Accepted" when Status is ShipmentStatus.Labeled => SetStatus(ShipmentStatus.InTransit),
            _ => false,
        };
    }

    private bool SetStatus(ShipmentStatus next)
    {
        if (Status == next) return false;
        Status = next;
        return true;
    }

    private void EnsureStatus(ShipmentStatus expected)
    {
        if (Status != expected)
            throw new InvalidOperationException($"Expected status {expected}, was {Status}.");
    }
}
