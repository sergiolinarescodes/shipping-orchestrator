using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShippingOrchestrator.ReadModels.Customer.Persistence;

public sealed class CustomerBatchEntity
{
    public Guid BatchId { get; set; }
    public Guid TenantId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ParcelCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

internal sealed class CustomerBatchConfiguration : IEntityTypeConfiguration<CustomerBatchEntity>
{
    public void Configure(EntityTypeBuilder<CustomerBatchEntity> builder)
    {
        builder.ToTable("batches");
        builder.HasKey(b => b.BatchId);
        builder.Property(b => b.BatchId).HasColumnName("batch_id");
        builder.Property(b => b.TenantId).HasColumnName("tenant_id");
        builder.Property(b => b.Status).HasColumnName("status").HasMaxLength(24);
        builder.Property(b => b.ParcelCount).HasColumnName("parcel_count");
        builder.Property(b => b.SuccessCount).HasColumnName("success_count");
        builder.Property(b => b.FailureCount).HasColumnName("failure_count");
        builder.Property(b => b.CreatedAt).HasColumnName("created_at");
        builder.Property(b => b.CompletedAt).HasColumnName("completed_at");
        builder.HasIndex(b => new { b.TenantId, b.CreatedAt });
        // Status-filtered ListBatches paginates by CreatedAt DESC; the (TenantId, Status, CreatedAt DESC)
        // ordering lets Postgres serve a single tenant's status slice without a separate sort.
        builder.HasIndex(b => new { b.TenantId, b.Status, b.CreatedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_customer_batches_tenant_status_created_desc");
    }
}
