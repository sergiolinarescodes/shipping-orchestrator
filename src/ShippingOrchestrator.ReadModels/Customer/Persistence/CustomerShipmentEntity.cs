using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShippingOrchestrator.ReadModels.Customer.Persistence;

public sealed class CustomerShipmentEntity
{
    public Guid ShipmentId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? BatchId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CarrierCode { get; set; }
    public string? TrackingNumber { get; set; }
    public string? LabelUri { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class CustomerShipmentConfiguration : IEntityTypeConfiguration<CustomerShipmentEntity>
{
    public void Configure(EntityTypeBuilder<CustomerShipmentEntity> builder)
    {
        builder.ToTable("shipments");
        builder.HasKey(s => s.ShipmentId);
        builder.Property(s => s.ShipmentId).HasColumnName("shipment_id");
        builder.Property(s => s.TenantId).HasColumnName("tenant_id");
        builder.Property(s => s.BatchId).HasColumnName("batch_id");
        builder.Property(s => s.Status).HasColumnName("status").HasMaxLength(32);
        builder.Property(s => s.CarrierCode).HasColumnName("carrier_code").HasMaxLength(64);
        builder.Property(s => s.TrackingNumber).HasColumnName("tracking_number").HasMaxLength(128);
        builder.Property(s => s.LabelUri).HasColumnName("label_uri").HasMaxLength(1024);
        builder.Property(s => s.FailureReason).HasColumnName("failure_reason").HasMaxLength(1024);
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(s => new { s.TenantId, s.CreatedAt });
        builder.HasIndex(s => s.BatchId);
        // Batch detail view orders shipments by CreatedAt; covers the lookup with a sorted scan.
        builder.HasIndex(s => new { s.BatchId, s.CreatedAt })
            .HasDatabaseName("ix_customer_shipments_batch_created");
    }
}
