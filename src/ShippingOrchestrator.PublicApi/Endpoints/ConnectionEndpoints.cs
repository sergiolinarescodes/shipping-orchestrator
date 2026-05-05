using ShippingOrchestrator.Application.Connections;
using ShippingOrchestrator.Domain.Tenancy;
using Wolverine;

namespace ShippingOrchestrator.PublicApi.Endpoints;

public static class ConnectionEndpoints
{
    public static void MapConnectionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/connections").WithTags("Connections");

        group.MapPost("/{platform}/install", async (
            string platform,
            StartOAuthHttpRequest request,
            IMessageBus bus,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var tenantId = tenantContext.Current
                ?? throw new InvalidOperationException("Tenant context is not set on this request.");
            var result = await bus.InvokeAsync<StartEcommerceOAuthResult>(
                new StartEcommerceOAuthCommand(tenantId, platform.ToLowerInvariant(),
                    request.ExternalAccountId, request.RedirectUri, request.State), ct)
                .ConfigureAwait(false);
            return Results.Ok(new StartConnectionInstallResponse(result.AuthorizationUrl));
        }).RequireAuthorization("Tenant").WithName("StartConnectionInstall");

        group.MapPost("/{platform}/callback", async (
            string platform,
            CompleteOAuthHttpRequest request,
            IMessageBus bus,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var tenantId = tenantContext.Current
                ?? throw new InvalidOperationException("Tenant context is not set on this request.");
            var result = await bus.InvokeAsync<CompleteEcommerceOAuthResult>(
                new CompleteEcommerceOAuthCommand(
                    tenantId,
                    platform.ToLowerInvariant(),
                    request.ExternalAccountId,
                    request.Code,
                    request.State,
                    request.AdditionalParameters ?? new Dictionary<string, string>()),
                ct)
                .ConfigureAwait(false);
            return Results.Ok(new CompleteConnectionInstallResponse(result.ConnectionId));
        }).RequireAuthorization("Tenant").WithName("CompleteConnectionInstall");
    }
}

public sealed record StartOAuthHttpRequest(string ExternalAccountId, string RedirectUri, string State);

public sealed record CompleteOAuthHttpRequest(
    string ExternalAccountId,
    string Code,
    string State,
    Dictionary<string, string>? AdditionalParameters);

public sealed record StartConnectionInstallResponse(string AuthorizationUrl);

public sealed record CompleteConnectionInstallResponse(Guid ConnectionId);
