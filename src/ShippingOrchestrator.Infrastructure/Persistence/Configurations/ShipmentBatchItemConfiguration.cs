using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingOrchestrator.Domain.Shipments;

namespace ShippingOrchestrator.Infrastructure.Persistence.Configurations;

internal sealed class ShipmentBatchItemConfiguration : IEntityTypeConfiguration<ShipmentBatchItem>
{
    public void Configure(EntityTypeBuilder<ShipmentBatchItem> builder)
    {
        builder.ToTable("shipment_batch_items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.BatchId).HasColumnName("batch_id");
        builder.Property(i => i.ShipmentId).HasColumnName("shipment_id");
        builder.Property(i => i.OrdinalIndex).HasColumnName("ordinal_index");
        builder.Property(i => i.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        builder.Property(i => i.FailureReason).HasColumnName("failure_reason").HasMaxLength(1024);
        builder.Property(i => i.ResolvedAt).HasColumnName("resolved_at");
        builder.HasIndex(i => new { i.BatchId, i.OrdinalIndex }).IsUnique();
        builder.HasIndex(i => i.ShipmentId);
    }
}
