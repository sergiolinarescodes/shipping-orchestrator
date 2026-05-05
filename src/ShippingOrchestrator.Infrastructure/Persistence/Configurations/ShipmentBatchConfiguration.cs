using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingOrchestrator.Domain.Shipments;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Infrastructure.Persistence.Configurations;

internal sealed class ShipmentBatchConfiguration : IEntityTypeConfiguration<ShipmentBatch>
{
    public void Configure(EntityTypeBuilder<ShipmentBatch> builder)
    {
        builder.ToTable("shipment_batches");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id");
        builder.Property(b => b.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(ValueConversions.TenantIdConverter);
        builder.Property(b => b.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(24);
        builder.Property(b => b.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(128)
            .HasConversion(ValueConversions.NullableIdempotencyKeyConverter);
        builder.Property(b => b.CreatedAt).HasColumnName("created_at");
        builder.Property(b => b.CompletedAt).HasColumnName("completed_at");
        builder.HasMany(b => b.Items)
            .WithOne()
            .HasForeignKey(i => i.BatchId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(b => new { b.TenantId, b.IdempotencyKey }).IsUnique();
        builder.HasIndex(b => new { b.TenantId, b.Status });
        // FK to tenants — same reasoning as ecommerce_connections: blocks orphan batches
        // from a stale SPA session referencing a deleted tenant. Restrict on delete so
        // tenant deletion forces cleanup of dependent batches first.
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(b => b.TenantId)
            .HasPrincipalKey(t => t.Id)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(b => b.DomainEvents);
    }
}
