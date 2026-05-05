using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Email;
using ShippingOrchestrator.Application.Common.Encryption;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Application.Identity;
using ShippingOrchestrator.Application.Shipments;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Infrastructure.Email;
using ShippingOrchestrator.Infrastructure.Encryption;
using ShippingOrchestrator.Application.Ingestion;
using ShippingOrchestrator.Infrastructure.Persistence;
using ShippingOrchestrator.Infrastructure.Persistence.Repositories;
using ShippingOrchestrator.Infrastructure.Persistence.Tenancy;
using ShippingOrchestrator.Infrastructure.Wolverine;
using ShippingOrchestrator.Modules.Abstractions;

namespace ShippingOrchestrator.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the cross-cutting infrastructure shared by all hosts: DbContext, repositories,
    /// unit-of-work, tenant context, encryption, and the connector registry. Telemetry, Wolverine,
    /// and per-host concerns (auth) are wired separately by each host's Program.cs.
    /// </summary>
    public static IServiceCollection AddOrchestratorCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Orchestrator")
            ?? throw new InvalidOperationException("ConnectionStrings:Orchestrator is required.");

        services.AddDbContextPool<OrchestratorDbContext>(opt =>
        {
            opt.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", OrchestratorDbContext.SchemaName);
            });
        });

        services.AddScoped<IUnitOfWork, TransactionalUnitOfWork>();

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IEcommerceConnectionRepository, EcommerceConnectionRepository>();
        services.AddScoped<ICarrierAssignmentRepository, CarrierAssignmentRepository>();
        services.AddScoped<IShipmentRepository, ShipmentRepository>();
        services.AddScoped<IShipmentBatchRepository, ShipmentBatchRepository>();
        services.AddScoped<IOnboardingProcessRepository, OnboardingProcessRepository>();
        services.AddScoped<IPendingEcommerceOrderRepository, PendingEcommerceOrderRepository>();
        services.AddScoped<IIngestionFailureRepository, IngestionFailureRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ITenantMembershipRepository, TenantMembershipRepository>();
        services.AddScoped<IMagicLinkTokenRepository, MagicLinkTokenRepository>();
        services.AddScoped<IAuthSessionRepository, AuthSessionRepository>();
        services.AddScoped<ITenantInvitationRepository, TenantInvitationRepository>();

        services.AddSingleton<AmbientTenantContext>();
        services.AddSingleton<ITenantContext>(sp => sp.GetRequiredService<AmbientTenantContext>());
        services.AddSingleton<TenantQueryFilter>();

        services.AddSingleton<ConnectorRegistry>();

        services.Configure<AesEnvelopeOptions>(configuration.GetSection("Encryption:Aes"));
        services.AddSingleton<IEnvelopeEncryptor, AesEnvelopeEncryptor>();

        services.Configure<ShipmentTrackingPollOptions>(
            configuration.GetSection(ShipmentTrackingPollOptions.SectionName));

        services.AddScoped<IIngestionDispatcher, IngestionDispatcher>();

        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.AddSingleton<IEmailSender, SmtpEmailSender>();

        return services;
    }
}
