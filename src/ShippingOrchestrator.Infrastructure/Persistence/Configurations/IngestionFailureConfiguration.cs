using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingOrchestrator.Domain.Ingestion;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Infrastructure.Persistence.Configurations;

internal sealed class IngestionFailureConfiguration : IEntityTypeConfiguration<IngestionFailure>
{
    public void Configure(EntityTypeBuilder<IngestionFailure> builder)
    {
        builder.ToTable("ingestion_failures");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(ValueConversions.TenantIdConverter);
        builder.Property(f => f.ConnectorCode).HasColumnName("connector_code").HasMaxLength(64).IsRequired();
        builder.Property(f => f.ExternalOrderId).HasColumnName("external_order_id").HasMaxLength(256);
        builder.Property(f => f.LookupKey).HasColumnName("lookup_key").HasMaxLength(256).IsRequired();
        builder.Property(f => f.ReasonCode).HasColumnName("reason_code").HasConversion<string>().HasMaxLength(48).IsRequired();
        builder.Property(f => f.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(f => f.Severity).HasColumnName("severity").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(f => f.Message).HasColumnName("message").HasMaxLength(2048).IsRequired();
        builder.Property(f => f.TenantHint).HasColumnName("tenant_hint").HasMaxLength(512).IsRequired();
        builder.Property(f => f.RawBodyExcerpt).HasColumnName("raw_body_excerpt").HasMaxLength(4096).IsRequired();
        builder.Property(f => f.RawBodyHash).HasColumnName("raw_body_hash").HasMaxLength(64).IsRequired();
        builder.Property(f => f.ContextJson).HasColumnName("context_json").HasColumnType("jsonb").IsRequired();
        builder.Property(f => f.OccurredAt).HasColumnName("occurred_at");
        builder.Property(f => f.LastOccurredAt).HasColumnName("last_occurred_at");
        builder.Property(f => f.OccurrenceCount).HasColumnName("occurrence_count");
        builder.Property(f => f.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(f => f.ResolvedReason).HasColumnName("resolved_reason").HasMaxLength(512);
        builder.Property(f => f.DismissedAt).HasColumnName("dismissed_at");
        builder.Property(f => f.DismissedBy).HasColumnName("dismissed_by").HasMaxLength(128);
        builder.Property(f => f.ExpiresAt).HasColumnName("expires_at");

        // Partial unique index: at most one Open row per (tenant, connector, lookupKey).
        // Resolved/Dismissed rows fall outside the filter so a fresh failure on the same
        // order can spawn a new Open row after it was previously resolved.
        builder.HasIndex(f => new { f.TenantId, f.ConnectorCode, f.LookupKey })
            .IsUnique()
            .HasFilter("status = 'Open'")
            .HasDatabaseName("ix_ingestion_failures_open_unique");

        builder.HasIndex(f => new { f.TenantId, f.Status });
        builder.HasIndex(f => f.OccurredAt);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(f => f.TenantId)
            .HasPrincipalKey(t => t.Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(f => f.DomainEvents);
    }
}
