using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingOrchestrator.Domain.Ingestion;

namespace ShippingOrchestrator.Infrastructure.Persistence.Configurations;

internal sealed class PendingEcommerceOrderConfiguration : IEntityTypeConfiguration<PendingEcommerceOrder>
{
    public void Configure(EntityTypeBuilder<PendingEcommerceOrder> builder)
    {
        builder.ToTable("pending_ecommerce_orders");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(ValueConversions.TenantIdConverter);
        builder.Property(p => p.PlatformCode).HasColumnName("platform_code").HasMaxLength(64).IsRequired();
        builder.Property(p => p.ExternalOrderId).HasColumnName("external_order_id").HasMaxLength(128).IsRequired();
        builder.Property(p => p.PayloadJson).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(p => p.IngestedAt).HasColumnName("ingested_at");
        builder.Property(p => p.ConsumedAt).HasColumnName("consumed_at");
        builder.Property(p => p.ConsumedByBatchId).HasColumnName("consumed_by_batch_id");

        // Idempotency for retries from the source platform: a Shopify retry of the same order
        // should land on the same row, not create a duplicate pending entry.
        builder.HasIndex(p => new { p.TenantId, p.PlatformCode, p.ExternalOrderId }).IsUnique();
        builder.HasIndex(p => new { p.TenantId, p.ConsumedAt });
    }
}
