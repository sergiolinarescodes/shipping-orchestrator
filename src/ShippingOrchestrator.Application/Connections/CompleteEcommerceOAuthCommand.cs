using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Encryption;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Application.Onboarding.Verification;
using ShippingOrchestrator.Domain.Connections;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Modules.Abstractions;
using ShippingOrchestrator.Modules.Abstractions.Ecommerce;

namespace ShippingOrchestrator.Application.Connections;

public sealed record CompleteEcommerceOAuthCommand(
    TenantId TenantId,
    string PlatformCode,
    string ExternalAccountId,
    string Code,
    string State,
    IReadOnlyDictionary<string, string> AdditionalParameters);

public sealed record CompleteEcommerceOAuthResult(Guid ConnectionId);

public static class CompleteEcommerceOAuthHandler
{
    public static async Task<CompleteEcommerceOAuthResult> Handle(
        CompleteEcommerceOAuthCommand command,
        ConnectorRegistry registry,
        IServiceProvider serviceProvider,
        IEcommerceConnectionRepository connections,
        IEnvelopeEncryptor encryptor,
        IVerificationProvider verificationProvider,
        IActivationPolicy activationPolicy,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var registration = registry.Get(command.PlatformCode);
        if (registration.Kind != ConnectorKind.Ecommerce)
            throw new InvalidOperationException($"Connector '{command.PlatformCode}' is not an ecommerce connector.");

        var connector = (IEcommerceConnector)registration.ConnectorFactory(serviceProvider);
        var oauthResult = await connector.CompleteOAuthAsync(
                new OAuthCallback(
                    command.TenantId,
                    command.ExternalAccountId,
                    command.Code,
                    command.State,
                    command.AdditionalParameters),
                cancellationToken)
            .ConfigureAwait(false);

        if (!oauthResult.Success || oauthResult.CredentialsPayload is null)
            throw new InvalidOperationException(
                $"OAuth completion failed: {oauthResult.FailureReason ?? "unknown"}");

        var encryptedCipher = await encryptor
            .EncryptAsync(oauthResult.CredentialsPayload, cancellationToken)
            .ConfigureAwait(false);

        // The connector returns a canonical form of the external id (e.g. WC trims slash and
        // lowercases the host). Use THAT for both lookup and persistence so a second install
        // pass with a slightly differently-typed store URL still resolves to the same row
        // instead of inserting a duplicate.
        var canonicalExternalAccountId = oauthResult.ExternalAccountId ?? command.ExternalAccountId;

        var existing = await connections.FindByExternalAccountAsync(
                command.TenantId, command.PlatformCode, canonicalExternalAccountId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            existing.RotateCredentials(encryptedCipher, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new CompleteEcommerceOAuthResult(existing.Id);
        }

        var connection = EcommerceConnection.Install(
            command.TenantId,
            command.PlatformCode,
            canonicalExternalAccountId,
            encryptedCipher,
            clock.UtcNow);

        var verification = await verificationProvider.VerifyAsync(
                new VerificationRequest(command.TenantId, command.PlatformCode, connection.ExternalAccountId),
                cancellationToken)
            .ConfigureAwait(false);
        var decision = activationPolicy.Decide(verification);
        if (decision.Activate)
            connection.MarkVerified(clock.UtcNow);
        else if (decision.RejectReason is not null)
            connection.MarkRejected(decision.RejectReason, clock.UtcNow);

        await connections.AddAsync(connection, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new CompleteEcommerceOAuthResult(connection.Id);
    }
}
