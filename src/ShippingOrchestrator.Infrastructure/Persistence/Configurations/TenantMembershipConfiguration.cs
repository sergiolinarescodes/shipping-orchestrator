using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingOrchestrator.Domain.Identity;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Infrastructure.Persistence.Configurations;

internal sealed class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.ToTable("tenant_memberships");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.AccountId)
            .HasColumnName("account_id")
            .HasConversion(IdentityValueConversions.AccountIdConverter);
        builder.Property(m => m.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(ValueConversions.TenantIdConverter);
        builder.Property(m => m.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(16);
        builder.Property(m => m.GrantedByAccountId)
            .HasColumnName("granted_by_account_id")
            .HasConversion(IdentityValueConversions.NullableAccountIdConverter);
        builder.Property(m => m.GrantedAt).HasColumnName("granted_at");

        builder.HasIndex(m => new { m.AccountId, m.TenantId }).IsUnique();
        builder.HasIndex(m => m.TenantId);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(m => m.AccountId)
            .HasPrincipalKey(a => a.Id)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(m => m.TenantId)
            .HasPrincipalKey(t => t.Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(m => m.DomainEvents);
    }
}
