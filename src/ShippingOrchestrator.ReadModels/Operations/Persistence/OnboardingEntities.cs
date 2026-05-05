using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShippingOrchestrator.ReadModels.Operations.Persistence;

public sealed class OpsOnboardingProcessEntity
{
    public Guid ProcessId { get; set; }
    public string FlowCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public string? StartedByStaffUserId { get; set; }
    public string? ContactEmail { get; set; }
    public string? CurrentStepCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

internal sealed class OpsOnboardingProcessConfiguration : IEntityTypeConfiguration<OpsOnboardingProcessEntity>
{
    public void Configure(EntityTypeBuilder<OpsOnboardingProcessEntity> b)
    {
        b.ToTable("onboarding_processes");
        b.HasKey(p => p.ProcessId);
        b.Property(p => p.ProcessId).HasColumnName("process_id");
        b.Property(p => p.FlowCode).HasColumnName("flow_code").HasMaxLength(64);
        b.Property(p => p.Status).HasColumnName("status").HasMaxLength(16);
        b.Property(p => p.TenantId).HasColumnName("tenant_id");
        b.Property(p => p.StartedByStaffUserId).HasColumnName("started_by_staff_user_id").HasMaxLength(128);
        b.Property(p => p.ContactEmail).HasColumnName("contact_email").HasMaxLength(254);
        b.Property(p => p.CurrentStepCode).HasColumnName("current_step_code").HasMaxLength(64);
        b.Property(p => p.CreatedAt).HasColumnName("created_at");
        b.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        b.Property(p => p.CompletedAt).HasColumnName("completed_at");
        b.HasIndex(p => new { p.FlowCode, p.Status });
        b.HasIndex(p => p.TenantId);
    }
}
