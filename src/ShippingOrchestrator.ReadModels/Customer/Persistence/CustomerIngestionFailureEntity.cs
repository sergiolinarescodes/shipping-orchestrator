using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShippingOrchestrator.ReadModels.Customer.Persistence;

public sealed class CustomerIngestionFailureEntity
{
    public Guid FailureId { get; set; }
    public Guid TenantId { get; set; }
    public string ConnectorCode { get; set; } = string.Empty;
    public string? ExternalOrderId { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string TenantHint { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset LastOccurredAt { get; set; }
    public int OccurrenceCount { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset? DismissedAt { get; set; }
}

internal sealed class CustomerIngestionFailureConfiguration : IEntityTypeConfiguration<CustomerIngestionFailureEntity>
{
    public void Configure(EntityTypeBuilder<CustomerIngestionFailureEntity> builder)
    {
        builder.ToTable("ingestion_failures");
        builder.HasKey(f => f.FailureId);
        builder.Property(f => f.FailureId).HasColumnName("failure_id");
        builder.Property(f => f.TenantId).HasColumnName("tenant_id");
        builder.Property(f => f.ConnectorCode).HasColumnName("connector_code").HasMaxLength(64);
        builder.Property(f => f.ExternalOrderId).HasColumnName("external_order_id").HasMaxLength(256);
        builder.Property(f => f.ReasonCode).HasColumnName("reason_code").HasMaxLength(48);
        builder.Property(f => f.Status).HasColumnName("status").HasMaxLength(24);
        builder.Property(f => f.Message).HasColumnName("message").HasMaxLength(2048);
        builder.Property(f => f.TenantHint).HasColumnName("tenant_hint").HasMaxLength(512);
        builder.Property(f => f.OccurredAt).HasColumnName("occurred_at");
        builder.Property(f => f.LastOccurredAt).HasColumnName("last_occurred_at");
        builder.Property(f => f.OccurrenceCount).HasColumnName("occurrence_count");
        builder.Property(f => f.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(f => f.DismissedAt).HasColumnName("dismissed_at");
        builder.HasIndex(f => new { f.TenantId, f.Status, f.OccurredAt })
            .HasDatabaseName("ix_customer_ingestion_failures_tenant_status_occurred");
        // ListIngestionFailures paginates by LastOccurredAt DESC (the rolling timestamp the
        // stats card reads), so the OccurredAt index above can't serve the order. This one
        // matches the customer dashboard query exactly.
        builder.HasIndex(f => new { f.TenantId, f.Status, f.LastOccurredAt })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_customer_ingestion_failures_tenant_status_last_desc");
    }
}
