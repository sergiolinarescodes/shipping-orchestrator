namespace ShippingOrchestrator.Domain.Ingestion;

/// <summary>
/// Write-only field in v1 — populated when the failure is raised but not yet rendered or
/// alerted on. Reserved for the eventual ops-paging integration so adding alerting later does
/// not require a write-side migration.
/// </summary>
public enum IngestionFailureSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2,
}
