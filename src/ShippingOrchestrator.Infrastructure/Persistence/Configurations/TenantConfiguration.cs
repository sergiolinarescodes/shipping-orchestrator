using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasConversion(ValueConversions.TenantIdConverter);
        builder.Property(t => t.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(t => t.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        builder.Property(t => t.ContactEmail).HasColumnName("contact_email").HasMaxLength(254);
        builder.Property(t => t.CarrierMode).HasColumnName("carrier_mode").HasConversion<string>().HasMaxLength(16);
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.OwnsOne(t => t.ToSAcceptance, tos =>
        {
            tos.Property(x => x.SignerName).HasColumnName("tos_signer_name").HasMaxLength(200);
            tos.Property(x => x.SignerEmail).HasColumnName("tos_signer_email").HasMaxLength(254);
            tos.Property(x => x.IpAddress).HasColumnName("tos_ip").HasMaxLength(45);
            tos.Property(x => x.ToSVersion).HasColumnName("tos_version").HasMaxLength(64);
            tos.Property(x => x.AcceptedAt).HasColumnName("tos_accepted_at");
        });
        builder.Ignore(t => t.DomainEvents);
    }
}
