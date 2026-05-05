using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShippingOrchestrator.ReadModels.Operations.Persistence;

public sealed class OpsShipmentTrackingEventEntity
{
    public Guid Id { get; set; }
    public Guid ShipmentId { get; set; }
    public int Sequence { get; set; }
    public string EventCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

internal sealed class OpsShipmentTrackingEventConfiguration : IEntityTypeConfiguration<OpsShipmentTrackingEventEntity>
{
    public void Configure(EntityTypeBuilder<OpsShipmentTrackingEventEntity> b)
    {
        b.ToTable("shipment_tracking_events");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id");
        b.Property(e => e.ShipmentId).HasColumnName("shipment_id");
        b.Property(e => e.Sequence).HasColumnName("sequence");
        b.Property(e => e.EventCode).HasColumnName("event_code").HasMaxLength(64);
        b.Property(e => e.Description).HasColumnName("description").HasMaxLength(1024);
        b.Property(e => e.Location).HasColumnName("location").HasMaxLength(256);
        b.Property(e => e.OccurredAt).HasColumnName("occurred_at");
        b.HasIndex(e => new { e.ShipmentId, e.Sequence }).IsUnique();
    }
}
