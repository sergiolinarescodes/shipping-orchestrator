using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShippingOrchestrator.ReadModels.Operations.Persistence;

public sealed class OpsIngestionFailureEntity
{
    public Guid FailureId { get; set; }
    public Guid TenantId { get; set; }
    public string ConnectorCode { get; set; } = string.Empty;
    public string? ExternalOrderId { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string TenantHint { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset LastOccurredAt { get; set; }
    public int OccurrenceCount { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolvedReason { get; set; }
    public DateTimeOffset? DismissedAt { get; set; }
    public string? DismissedBy { get; set; }
}

internal sealed class OpsIngestionFailureConfiguration : IEntityTypeConfiguration<OpsIngestionFailureEntity>
{
    public void Configure(EntityTypeBuilder<OpsIngestionFailureEntity> b)
    {
        b.ToTable("ingestion_failures");
        b.HasKey(f => f.FailureId);
        b.Property(f => f.FailureId).HasColumnName("failure_id");
        b.Property(f => f.TenantId).HasColumnName("tenant_id");
        b.Property(f => f.ConnectorCode).HasColumnName("connector_code").HasMaxLength(64);
        b.Property(f => f.ExternalOrderId).HasColumnName("external_order_id").HasMaxLength(256);
        b.Property(f => f.ReasonCode).HasColumnName("reason_code").HasMaxLength(48);
        b.Property(f => f.Status).HasColumnName("status").HasMaxLength(24);
        b.Property(f => f.Severity).HasColumnName("severity").HasMaxLength(16);
        b.Property(f => f.Message).HasColumnName("message").HasMaxLength(2048);
        b.Property(f => f.TenantHint).HasColumnName("tenant_hint").HasMaxLength(512);
        b.Property(f => f.OccurredAt).HasColumnName("occurred_at");
        b.Property(f => f.LastOccurredAt).HasColumnName("last_occurred_at");
        b.Property(f => f.OccurrenceCount).HasColumnName("occurrence_count");
        b.Property(f => f.ResolvedAt).HasColumnName("resolved_at");
        b.Property(f => f.ResolvedReason).HasColumnName("resolved_reason").HasMaxLength(512);
        b.Property(f => f.DismissedAt).HasColumnName("dismissed_at");
        b.Property(f => f.DismissedBy).HasColumnName("dismissed_by").HasMaxLength(128);
        b.HasIndex(f => new { f.TenantId, f.Status })
            .HasDatabaseName("ix_ops_ingestion_failures_tenant_status");
        b.HasIndex(f => new { f.Status, f.ReasonCode, f.LastOccurredAt })
            .HasDatabaseName("ix_ops_ingestion_failures_status_reason_last");
        // Tenant-scoped pagination by LastOccurredAt DESC (operator console default order).
        b.HasIndex(f => new { f.TenantId, f.LastOccurredAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_ops_ingestion_failures_tenant_last_desc");
        // Stats card reads "since X"; single-column index serves the time-window scan in
        // either direction (Postgres scans backward for DESC queries at full speed).
        b.HasIndex(f => f.LastOccurredAt)
            .HasDatabaseName("ix_ops_ingestion_failures_last");
    }
}
