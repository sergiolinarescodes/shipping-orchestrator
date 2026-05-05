using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingOrchestrator.Domain.Identity;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Infrastructure.Persistence.Configurations;

internal sealed class TenantInvitationConfiguration : IEntityTypeConfiguration<TenantInvitation>
{
    public void Configure(EntityTypeBuilder<TenantInvitation> builder)
    {
        builder.ToTable("tenant_invitations");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(ValueConversions.TenantIdConverter);
        builder.Property(i => i.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
        builder.Property(i => i.InvitedByAccountId)
            .HasColumnName("invited_by_account_id")
            .HasConversion(IdentityValueConversions.AccountIdConverter);
        builder.Property(i => i.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(16);
        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.Property(i => i.ConsumedAt).HasColumnName("consumed_at");
        builder.Property(i => i.RevokedAt).HasColumnName("revoked_at");

        builder.HasIndex(i => new { i.Email, i.TenantId });
        builder.HasIndex(i => i.TenantId);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(i => i.TenantId)
            .HasPrincipalKey(t => t.Id)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(i => i.InvitedByAccountId)
            .HasPrincipalKey(a => a.Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(i => i.DomainEvents);
    }
}
