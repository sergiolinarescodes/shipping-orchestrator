using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingOrchestrator.Domain.Connections;

namespace ShippingOrchestrator.Infrastructure.Persistence.Configurations;

internal sealed class CarrierAssignmentConfiguration : IEntityTypeConfiguration<CarrierAssignment>
{
    public void Configure(EntityTypeBuilder<CarrierAssignment> builder)
    {
        builder.ToTable("carrier_assignments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(ValueConversions.TenantIdConverter);
        builder.Property(a => a.CarrierCode).HasColumnName("carrier_code").HasMaxLength(64).IsRequired();
        builder.Property(a => a.Priority).HasColumnName("priority");
        builder.Property(a => a.IsActive).HasColumnName("is_active");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");

        // string[] maps natively to Postgres text[] via the Npgsql provider, no converter required.
        builder.Property(a => a.OriginCountries).HasColumnName("origin_countries").HasColumnType("text[]");
        builder.Property(a => a.DestinationCountries).HasColumnName("destination_countries").HasColumnType("text[]");

        builder.HasIndex(a => new { a.TenantId, a.CarrierCode }).IsUnique();
        builder.HasIndex(a => a.TenantId);
        builder.Ignore(a => a.DomainEvents);
        builder.Ignore(a => a.Origins);
        builder.Ignore(a => a.Destinations);
    }
}
