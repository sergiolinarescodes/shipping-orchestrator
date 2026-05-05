using System.Text.Json;
using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Ingestion;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Application.Ingestion;

/// <summary>
/// Records a webhook-translation failure. Idempotent by <c>(TenantId, ConnectorCode, LookupKey)</c>:
/// if an Open row already exists for that key, the same row is updated rather than a new one
/// being inserted. A 60-second cooldown gates emission of <c>IngestionFailureReoccurred</c>
/// so a misconfigured store firing 10k webhooks per minute doesn't thrash the projection
/// stream — the handler still bumps <c>OccurrenceCount</c> in the cooldown window so the
/// write side stays accurate.
/// </summary>
public sealed record RecordIngestionFailureCommand(
    TenantId TenantId,
    string ConnectorCode,
    string? ExternalOrderId,
    IngestionReasonCode ReasonCode,
    string Message,
    string TenantHint,
    string RawBodyExcerpt,
    string RawBodyHash,
    IReadOnlyDictionary<string, string>? Context = null,
    IngestionFailureSeverity Severity = IngestionFailureSeverity.Warning);

public sealed record RecordIngestionFailureResult(Guid FailureId, bool WasNew, int OccurrenceCount);

public static class RecordIngestionFailureHandler
{
    public static readonly TimeSpan ReoccurEventCooldown = TimeSpan.FromSeconds(60);

    public static async Task<RecordIngestionFailureResult> Handle(
        RecordIngestionFailureCommand command,
        IIngestionFailureRepository repository,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ConnectorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Message);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.RawBodyHash);

        var now = clock.UtcNow;
        var connector = command.ConnectorCode.ToLowerInvariant();
        var lookupKey = string.IsNullOrWhiteSpace(command.ExternalOrderId)
            ? $"hash:{command.RawBodyHash}"
            : command.ExternalOrderId;
        var contextJson = command.Context is null || command.Context.Count == 0
            ? null
            : JsonSerializer.Serialize(command.Context);

        var existing = await repository
            .FindOpenByLookupKeyAsync(command.TenantId, connector, lookupKey, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            var sameReason = existing.ReasonCode == command.ReasonCode;
            var inCooldown = now - existing.LastOccurredAt < ReoccurEventCooldown;
            if (sameReason && inCooldown)
                existing.BumpOccurrenceCount(now);
            else
                existing.Reoccur(command.ReasonCode, command.Message, command.TenantHint, contextJson, now);

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new RecordIngestionFailureResult(existing.Id, WasNew: false, existing.OccurrenceCount);
        }

        var fresh = IngestionFailure.Raise(
            command.TenantId,
            connector,
            command.ExternalOrderId,
            command.ReasonCode,
            command.Message,
            command.TenantHint,
            command.RawBodyExcerpt ?? string.Empty,
            command.RawBodyHash,
            contextJson,
            command.Severity,
            now);

        await repository.AddAsync(fresh, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new RecordIngestionFailureResult(fresh.Id, WasNew: true, fresh.OccurrenceCount);
    }
}
