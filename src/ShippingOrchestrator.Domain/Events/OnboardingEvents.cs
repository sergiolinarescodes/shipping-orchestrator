using ShippingOrchestrator.Domain.Abstractions;
using ShippingOrchestrator.Domain.Onboarding;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Domain.Events;

public sealed record OnboardingProcessStarted(
    OnboardingProcessId ProcessId,
    string FlowCode,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OnboardingStepAwaiting(
    OnboardingProcessId ProcessId,
    string StepCode,
    string ExternalCorrelationId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OnboardingStepCompleted(
    OnboardingProcessId ProcessId,
    string StepCode,
    int Sequence,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OnboardingStepFailed(
    OnboardingProcessId ProcessId,
    string StepCode,
    string Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OnboardingProcessCompleted(
    OnboardingProcessId ProcessId,
    TenantId? TenantId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OnboardingProcessCancelled(
    OnboardingProcessId ProcessId,
    string? Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OnboardingProcessTimedOut(
    OnboardingProcessId ProcessId,
    string StepCode,
    DateTimeOffset OccurredAt) : IDomainEvent;
