using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingOrchestrator.Domain.Connections;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Infrastructure.Persistence.Configurations;

internal sealed class EcommerceConnectionConfiguration : IEntityTypeConfiguration<EcommerceConnection>
{
    public void Configure(EntityTypeBuilder<EcommerceConnection> builder)
    {
        builder.ToTable("ecommerce_connections");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(ValueConversions.TenantIdConverter);
        builder.Property(c => c.PlatformCode).HasColumnName("platform_code").HasMaxLength(64).IsRequired();
        builder.Property(c => c.ExternalAccountId).HasColumnName("external_account_id").HasMaxLength(256);
        builder.Property(c => c.CredentialsCipher).HasColumnName("credentials_cipher").HasColumnType("bytea");
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(c => c.InstalledAt).HasColumnName("installed_at");
        builder.Property(c => c.LastSyncAt).HasColumnName("last_sync_at");
        builder.Property(c => c.VerifiedAt).HasColumnName("verified_at");
        builder.Property(c => c.RejectedAt).HasColumnName("rejected_at");
        builder.Property(c => c.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(512);
        builder.Property(c => c.DisconnectedAt).HasColumnName("disconnected_at");
        builder.Property(c => c.DisconnectReason).HasColumnName("disconnect_reason").HasMaxLength(512);
        builder.HasIndex(c => new { c.TenantId, c.PlatformCode, c.ExternalAccountId }).IsUnique();
        builder.HasIndex(c => c.TenantId);
        // FK to tenants. Restrict so a tenant can't be deleted while it owns connections —
        // forces explicit cleanup, matches the intent of "the tenant must exist for the
        // connection to be valid". Without this FK, SPA-side stale state (a localStorage
        // tenant id surviving a DB wipe) would create rows referencing a phantom tenant.
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .HasPrincipalKey(t => t.Id)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(c => c.DomainEvents);
    }
}
