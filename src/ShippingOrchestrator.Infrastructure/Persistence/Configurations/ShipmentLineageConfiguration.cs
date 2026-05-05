using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingOrchestrator.Domain.Shipments;

namespace ShippingOrchestrator.Infrastructure.Persistence.Configurations;

internal sealed class ShipmentLineageConfiguration : IEntityTypeConfiguration<ShipmentLineage>
{
    public void Configure(EntityTypeBuilder<ShipmentLineage> builder)
    {
        builder.ToTable("shipment_lineage");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(l => l.ShipmentId).HasColumnName("shipment_id");
        builder.Property(l => l.FromStatus).HasColumnName("from_status").HasConversion<string>().HasMaxLength(32);
        builder.Property(l => l.ToStatus).HasColumnName("to_status").HasConversion<string>().HasMaxLength(32);
        builder.Property(l => l.Actor).HasColumnName("actor").HasMaxLength(128);
        builder.Property(l => l.Reason).HasColumnName("reason").HasMaxLength(1024);
        builder.Property(l => l.RuleAttribution).HasColumnName("rule_attribution").HasMaxLength(1024);
        builder.Property(l => l.OccurredAt).HasColumnName("occurred_at");
        builder.HasIndex(l => l.ShipmentId);
    }
}
