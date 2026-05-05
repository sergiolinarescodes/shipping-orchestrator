using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingOrchestrator.Domain.Identity;

namespace ShippingOrchestrator.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasColumnName("id")
            .HasConversion(IdentityValueConversions.AccountIdConverter);
        builder.Property(a => a.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
        builder.Property(a => a.DisplayName).HasColumnName("display_name").HasMaxLength(200);
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.LastSignInAt).HasColumnName("last_sign_in_at");

        builder.HasIndex(a => a.Email).IsUnique();

        builder.Ignore(a => a.DomainEvents);
    }
}
