using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingOrchestrator.Domain.Identity;

namespace ShippingOrchestrator.Infrastructure.Persistence.Configurations;

internal sealed class AuthSessionConfiguration : IEntityTypeConfiguration<AuthSession>
{
    public void Configure(EntityTypeBuilder<AuthSession> builder)
    {
        builder.ToTable("auth_sessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.SessionHash).HasColumnName("session_hash").HasMaxLength(128).IsRequired();
        builder.Property(s => s.AccountId)
            .HasColumnName("account_id")
            .HasConversion(IdentityValueConversions.AccountIdConverter);
        builder.Property(s => s.CurrentTenantId)
            .HasColumnName("current_tenant_id")
            .HasConversion(ValueConversions.NullableTenantIdConverter);
        builder.Property(s => s.IssuedAt).HasColumnName("issued_at");
        builder.Property(s => s.ExpiresAt).HasColumnName("expires_at");
        builder.Property(s => s.LastSeenAt).HasColumnName("last_seen_at");
        builder.Property(s => s.RevokedAt).HasColumnName("revoked_at");

        builder.HasIndex(s => s.SessionHash).IsUnique();
        builder.HasIndex(s => s.AccountId);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(s => s.AccountId)
            .HasPrincipalKey(a => a.Id)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(s => s.DomainEvents);
    }
}
