using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingOrchestrator.Domain.Onboarding;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Infrastructure.Persistence.Configurations;

internal sealed class OnboardingProcessConfiguration : IEntityTypeConfiguration<OnboardingProcess>
{
    public void Configure(EntityTypeBuilder<OnboardingProcess> builder)
    {
        builder.ToTable("onboarding_processes");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasConversion(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<OnboardingProcessId, Guid>(
                v => v.Value, v => new OnboardingProcessId(v)));
        builder.Property(p => p.FlowCode).HasColumnName("flow_code").HasMaxLength(64).IsRequired();
        builder.Property(p => p.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        builder.Property(p => p.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<TenantId?, Guid?>(
                v => v == null ? null : v.Value.Value,
                v => v == null ? null : new TenantId(v.Value)));
        builder.Property(p => p.StartedByStaffUserId).HasColumnName("started_by_staff_user_id").HasMaxLength(128);
        builder.Property(p => p.ContactEmail).HasColumnName("contact_email").HasMaxLength(254);
        builder.Property(p => p.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion();
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.CompletedAt).HasColumnName("completed_at");

        builder.HasMany(p => p.Steps)
            .WithOne()
            .HasForeignKey(s => s.ProcessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.FlowCode, p.Status });
        builder.HasIndex(p => p.TenantId);
        builder.Ignore(p => p.DomainEvents);
    }
}

internal sealed class OnboardingStepRecordConfiguration : IEntityTypeConfiguration<OnboardingStepRecord>
{
    public void Configure(EntityTypeBuilder<OnboardingStepRecord> builder)
    {
        builder.ToTable("onboarding_steps");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.ProcessId)
            .HasColumnName("process_id")
            .HasConversion(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<OnboardingProcessId, Guid>(
                v => v.Value, v => new OnboardingProcessId(v)));
        builder.Property(s => s.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
        builder.Property(s => s.Sequence).HasColumnName("sequence");
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        builder.Property(s => s.CollectedPayload).HasColumnName("collected_payload").HasColumnType("jsonb");
        builder.Property(s => s.ResultPayload).HasColumnName("result_payload").HasColumnType("jsonb");
        builder.Property(s => s.FailureReason).HasColumnName("failure_reason").HasMaxLength(2048);
        builder.Property(s => s.ExternalCorrelationId).HasColumnName("external_correlation_id").HasMaxLength(256);
        builder.Property(s => s.AwaitingExpiresAt).HasColumnName("awaiting_expires_at");
        builder.Property(s => s.StartedAt).HasColumnName("started_at");
        builder.Property(s => s.CompletedAt).HasColumnName("completed_at");

        builder.HasIndex(s => new { s.ProcessId, s.Code }).IsUnique();
        builder.HasIndex(s => s.ExternalCorrelationId);
    }
}
