using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingOrchestrator.Domain.Shipments;

namespace ShippingOrchestrator.Infrastructure.Persistence.Configurations;

internal sealed class ShipmentTrackingEventConfiguration : IEntityTypeConfiguration<ShipmentTrackingEvent>
{
    public void Configure(EntityTypeBuilder<ShipmentTrackingEvent> builder)
    {
        builder.ToTable("shipment_tracking_events");
        builder.HasKey(e => e.Id);
        // ValueGeneratedNever: the domain factory (`ShipmentTrackingEvent.Create`) sets the id
        // explicitly. Without this, EF's default `ValueGeneratedOnAdd` for Guid PKs uses the
        // key value to infer entity state when the entity is discovered via navigation on an
        // already-tracked parent — a pre-set non-empty Guid is read as "Modified", and the
        // resulting UPDATE matches no row (the row doesn't exist yet) → DbUpdateConcurrencyException.
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.ShipmentId).HasColumnName("shipment_id");
        builder.Property(e => e.Sequence).HasColumnName("sequence");
        builder.Property(e => e.EventCode).HasColumnName("event_code").HasMaxLength(64).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(1024);
        builder.Property(e => e.Location).HasColumnName("location").HasMaxLength(256);
        builder.Property(e => e.OccurredAt).HasColumnName("occurred_at");
        builder.HasIndex(e => new { e.ShipmentId, e.Sequence }).IsUnique();
        builder.HasIndex(e => e.OccurredAt);
    }
}
