using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingOrchestrator.Domain.Shipments;
using ShippingOrchestrator.Domain.ValueObjects;

namespace ShippingOrchestrator.Infrastructure.Persistence.Configurations;

internal sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("shipments");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(ValueConversions.TenantIdConverter);
        builder.Property(s => s.BatchId).HasColumnName("batch_id");
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32);
        builder.Property(s => s.CarrierCode).HasColumnName("carrier_code").HasMaxLength(64);
        builder.Property(s => s.TrackingNumber).HasColumnName("tracking_number").HasMaxLength(128);
        builder.Property(s => s.LabelUri).HasColumnName("label_uri").HasMaxLength(1024);
        builder.Property(s => s.FailureReason).HasColumnName("failure_reason").HasMaxLength(1024);
        builder.Property(s => s.PreferredService)
            .HasColumnName("preferred_service")
            .HasMaxLength(32)
            .HasConversion(
                v => v == null ? null : v.Value.Code,
                v => v == null ? null : new ServiceLevel(v));
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");

        builder.Property(s => s.From)
            .HasColumnName("from_address")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Address>(v, JsonOptions)!);
        builder.Property(s => s.To)
            .HasColumnName("to_address")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Address>(v, JsonOptions)!);
        builder.Property(s => s.Parcel)
            .HasColumnName("parcel")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Parcel>(v, JsonOptions)!);

        builder.HasIndex(s => new { s.TenantId, s.Status });
        builder.HasIndex(s => s.BatchId);
        builder.Ignore(s => s.DomainEvents);

        builder.HasMany(s => s.TrackingEvents)
            .WithOne()
            .HasForeignKey(e => e.ShipmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
