using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShippingOrchestrator.ReadModels.Operations.Persistence;

public sealed class OpsTenantEntity
{
    public Guid TenantId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class OpsBatchEntity
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

public sealed class OpsShipmentEntity
{
    public Guid ShipmentId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? BatchId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CarrierCode { get; set; }
    public string? TrackingNumber { get; set; }
    public string? FailureReason { get; set; }
    public string CountryFrom { get; set; } = string.Empty;
    public string CountryTo { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class OpsCarrierDailyKpiEntity
{
    public string CarrierCode { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public double TotalLabelDurationMs { get; set; }
}

internal sealed class OpsTenantConfiguration : IEntityTypeConfiguration<OpsTenantEntity>
{
    public void Configure(EntityTypeBuilder<OpsTenantEntity> b)
    {
        b.ToTable("tenants");
        b.HasKey(t => t.TenantId);
        b.Property(t => t.TenantId).HasColumnName("tenant_id");
        b.Property(t => t.DisplayName).HasColumnName("display_name").HasMaxLength(200);
        b.Property(t => t.Status).HasColumnName("status").HasMaxLength(16);
        b.Property(t => t.CreatedAt).HasColumnName("created_at");
    }
}

internal sealed class OpsBatchConfiguration : IEntityTypeConfiguration<OpsBatchEntity>
{
    public void Configure(EntityTypeBuilder<OpsBatchEntity> b)
    {
        b.ToTable("batches");
        b.HasKey(x => x.BatchId);
        b.Property(x => x.BatchId).HasColumnName("batch_id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(24);
        b.Property(x => x.ParcelCount).HasColumnName("parcel_count");
        b.Property(x => x.SuccessCount).HasColumnName("success_count");
        b.Property(x => x.FailureCount).HasColumnName("failure_count");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.CompletedAt).HasColumnName("completed_at");
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.CreatedAt);
        // Status-filtered ListBatches: covers status slice + DESC pagination without a sort.
        b.HasIndex(x => new { x.Status, x.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_ops_batches_status_created_desc");
    }
}

internal sealed class OpsShipmentConfiguration : IEntityTypeConfiguration<OpsShipmentEntity>
{
    public void Configure(EntityTypeBuilder<OpsShipmentEntity> b)
    {
        b.ToTable("shipments");
        b.HasKey(s => s.ShipmentId);
        b.Property(s => s.ShipmentId).HasColumnName("shipment_id");
        b.Property(s => s.TenantId).HasColumnName("tenant_id");
        b.Property(s => s.BatchId).HasColumnName("batch_id");
        b.Property(s => s.Status).HasColumnName("status").HasMaxLength(32);
        b.Property(s => s.CarrierCode).HasColumnName("carrier_code").HasMaxLength(64);
        b.Property(s => s.TrackingNumber).HasColumnName("tracking_number").HasMaxLength(128);
        b.Property(s => s.FailureReason).HasColumnName("failure_reason").HasMaxLength(1024);
        b.Property(s => s.CountryFrom).HasColumnName("country_from").HasMaxLength(2);
        b.Property(s => s.CountryTo).HasColumnName("country_to").HasMaxLength(2);
        b.Property(s => s.CreatedAt).HasColumnName("created_at");
        b.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(s => s.Status);
        b.HasIndex(s => s.CarrierCode);
        // Partial index for the exceptions feed — ListExceptionsAsync filters by
        // (Status='Failed' OR FailureReason IS NOT NULL) and orders by UpdatedAt DESC. The
        // partial covers exactly that predicate so the exceptions page doesn't scan healthy
        // shipments. ASC vs DESC is irrelevant on a single-column Postgres index — the
        // planner scans in reverse for DESC queries at full speed. Predicate uses snake_case
        // column names because Postgres applies it on the raw table.
        b.HasIndex(s => s.UpdatedAt)
            .HasFilter("\"status\" = 'Failed' OR \"failure_reason\" IS NOT NULL")
            .HasDatabaseName("ix_ops_shipments_exceptions_updated");
    }
}

internal sealed class OpsCarrierDailyKpiConfiguration : IEntityTypeConfiguration<OpsCarrierDailyKpiEntity>
{
    public void Configure(EntityTypeBuilder<OpsCarrierDailyKpiEntity> b)
    {
        b.ToTable("carrier_daily_kpis");
        b.HasKey(k => new { k.CarrierCode, k.Date });
        b.Property(k => k.CarrierCode).HasColumnName("carrier_code").HasMaxLength(64);
        b.Property(k => k.Date).HasColumnName("date");
        b.Property(k => k.SuccessCount).HasColumnName("success_count");
        b.Property(k => k.FailureCount).HasColumnName("failure_count");
        b.Property(k => k.TotalLabelDurationMs).HasColumnName("total_label_duration_ms");
    }
}
