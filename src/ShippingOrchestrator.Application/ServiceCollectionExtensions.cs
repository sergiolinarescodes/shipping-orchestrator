using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Ingestion;
using ShippingOrchestrator.Application.Onboarding;
using ShippingOrchestrator.Application.Onboarding.Flows;
using ShippingOrchestrator.Application.Onboarding.Verification;
using ShippingOrchestrator.Application.Routing;
using ShippingOrchestrator.Application.Routing.Rules;

namespace ShippingOrchestrator.Application;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the orchestration application layer: clock, validators, routing engine + rules,
    /// and onboarding flow registry. Repositories and the unit of work are registered by the
    /// Infrastructure layer.
    /// </summary>
    public static IServiceCollection AddOrchestratorApplication(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();

        services.AddValidatorsFromAssemblyContaining<MarkerForApplicationAssembly>(includeInternalTypes: true);

        services.AddSingleton<ICarrierRoutingRule, CountryAllowedRule>();
        services.AddSingleton<ICarrierRoutingRule, PriorityRule>();
        services.AddScoped<RoutingEngine>();

        services.AddSingleton<IOnboardingFlow, ManualStaffOnboardingFlow>();
        services.AddSingleton<IOnboardingFlowRegistry, OnboardingFlowRegistry>();
        services.AddScoped<IOnboardingStepDispatcher, OnboardingStepDispatcher>();

        services.AddSingleton<IVerificationProvider, AutoPassVerificationProvider>();
        services.AddSingleton<IActivationPolicy, DefaultActivationPolicy>();

        services.AddSingleton<IEcommerceOrderTranslatorRegistry, EcommerceOrderTranslatorRegistry>();

        services.AddSingleton<IRawBodyRedactor, DefaultRawBodyRedactor>();

        return services;
    }
}

internal sealed class MarkerForApplicationAssembly;
