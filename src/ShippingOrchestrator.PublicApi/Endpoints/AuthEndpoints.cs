using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ShippingOrchestrator.Application.Identity;
using ShippingOrchestrator.Domain.Identity;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.PublicApi.Authentication;
using Wolverine;

namespace ShippingOrchestrator.PublicApi.Endpoints;

/// <summary>
/// Magic-link sign-in surface. Anonymous: <c>request-link</c>, <c>verify</c>. Account-scoped:
/// <c>me</c>, <c>select-tenant</c>, <c>sign-out</c>. Tenant-scoped: <c>invitations</c>.
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/auth").WithTags("Auth");

        group.MapPost("/request-link", async (
            RequestLinkBody body,
            HttpContext http,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Email))
                return Results.Accepted();

            var ipHash = HashRemoteIp(http);
            await bus.InvokeAsync<RequestMagicLinkResult>(
                new RequestMagicLinkCommand(body.Email, ipHash), ct).ConfigureAwait(false);
            return Results.Accepted();
        }).AllowAnonymous().WithName("RequestMagicLink");

        group.MapGet("/verify", async (
            string token,
            HttpContext http,
            IMessageBus bus,
            IOptions<AuthOptions> authOptions,
            IHostEnvironment env,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(token))
                return Results.Redirect(BuildLoginRedirect(authOptions.Value, "missing-token"));

            var result = await bus.InvokeAsync<ConsumeMagicLinkResult>(
                new ConsumeMagicLinkCommand(token), ct).ConfigureAwait(false);

            if (!result.Success || result.RawSessionToken is null || result.SessionExpiresAt is null)
                return Results.Redirect(BuildLoginRedirect(authOptions.Value, result.FailureReason ?? "invalid"));

            WriteSessionCookie(http, env, authOptions.Value, result.RawSessionToken, result.SessionExpiresAt.Value);

            var dashboard = authOptions.Value.DashboardBaseUrl.TrimEnd('/');
            return Results.Redirect($"{dashboard}/select-tenant");
        }).AllowAnonymous().WithName("VerifyMagicLink");

        group.MapGet("/me", async (
            HttpContext http,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            if (!TryGetSessionId(http.User, out var sessionId))
                return Results.Unauthorized();

            var view = await bus.InvokeAsync<SessionAccountView?>(
                new GetSessionAccountQuery(sessionId), ct).ConfigureAwait(false);
            if (view is null) return Results.Unauthorized();

            return Results.Ok(new SessionMeResponse(
                new SessionAccountDto(view.AccountId.Value, view.Email, view.DisplayName),
                view.CurrentTenantId?.Value,
                [.. view.Tenants.Select(t => new SessionTenantDto(
                    t.TenantId.Value, t.DisplayName, t.Status, t.Role.ToString()))]));
        }).RequireAuthorization(PublicApiPipeline.AccountPolicy).WithName("AuthMe");

        group.MapPost("/select-tenant", async (
            SelectTenantBody body,
            HttpContext http,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            if (!TryGetSessionId(http.User, out var sessionId))
                return Results.Unauthorized();
            if (body.TenantId == Guid.Empty)
                return Results.BadRequest(new { error = "tenantId is required." });

            var result = await bus.InvokeAsync<SelectTenantResult>(
                new SelectTenantCommand(sessionId, new TenantId(body.TenantId)), ct).ConfigureAwait(false);
            if (!result.Success)
                return Results.Problem(
                    title: result.FailureReason ?? "select-tenant-failed",
                    statusCode: StatusCodes.Status403Forbidden);
            return Results.NoContent();
        }).RequireAuthorization(PublicApiPipeline.AccountPolicy).WithName("SelectTenant");

        group.MapPost("/sign-out", async (
            HttpContext http,
            IMessageBus bus,
            IOptions<AuthOptions> authOptions,
            IHostEnvironment env,
            CancellationToken ct) =>
        {
            if (TryGetSessionId(http.User, out var sessionId))
                await bus.InvokeAsync<SignOutResult>(new SignOutCommand(sessionId), ct).ConfigureAwait(false);
            ClearSessionCookie(http, env, authOptions.Value);
            return Results.NoContent();
        }).RequireAuthorization(PublicApiPipeline.AccountPolicy).WithName("SignOut");

        group.MapPost("/invitations", async (
            InviteBody body,
            HttpContext http,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            if (!TryGetAccountId(http.User, out var accountId))
                return Results.Unauthorized();
            if (!TryGetTenantId(http.User, out var tenantId))
                return Results.Forbid();
            if (string.IsNullOrWhiteSpace(body.Email))
                return Results.BadRequest(new { error = "email is required." });

            var role = string.Equals(body.Role, "Owner", StringComparison.OrdinalIgnoreCase)
                ? MembershipRole.Owner
                : MembershipRole.Member;
            var result = await bus.InvokeAsync<InviteToTenantResult>(
                new InviteToTenantCommand(accountId, tenantId, body.Email, role), ct).ConfigureAwait(false);
            if (!result.Success)
                return Results.Problem(
                    title: result.FailureReason ?? "invite-failed",
                    statusCode: StatusCodes.Status403Forbidden);
            return Results.Ok(new { invitationId = result.InvitationId });
        }).RequireAuthorization(PublicApiPipeline.TenantPolicy).WithName("InviteToTenant");
    }

    private static void WriteSessionCookie(
        HttpContext http,
        IHostEnvironment env,
        AuthOptions options,
        string rawToken,
        DateTimeOffset expiresAt)
    {
        var name = SessionAuthHandler.ResolveCookieName(env, options);
        var cookieOptions = SessionAuthHandler.BuildCookieOptions(env, options, expiresAt);
        http.Response.Cookies.Append(name, rawToken, cookieOptions);
    }

    private static void ClearSessionCookie(HttpContext http, IHostEnvironment env, AuthOptions options)
    {
        var name = SessionAuthHandler.ResolveCookieName(env, options);
        var cookieOptions = SessionAuthHandler.BuildCookieOptions(env, options, DateTimeOffset.UnixEpoch);
        http.Response.Cookies.Append(name, string.Empty, cookieOptions);
    }

    private static string BuildLoginRedirect(AuthOptions options, string error) =>
        $"{options.DashboardBaseUrl.TrimEnd('/')}/login?error={Uri.EscapeDataString(error)}";

    private static bool TryGetSessionId(ClaimsPrincipal user, out Guid sessionId)
    {
        var raw = user.FindFirstValue(SessionAuthHandler.SessionIdClaim);
        return Guid.TryParse(raw, out sessionId);
    }

    private static bool TryGetAccountId(ClaimsPrincipal user, out AccountId accountId)
    {
        var raw = user.FindFirstValue(SessionAuthHandler.AccountIdClaim);
        if (Guid.TryParse(raw, out var guid))
        {
            accountId = new AccountId(guid);
            return true;
        }
        accountId = default;
        return false;
    }

    private static bool TryGetTenantId(ClaimsPrincipal user, out TenantId tenantId)
    {
        var raw = user.FindFirstValue(SessionAuthHandler.TenantIdClaim);
        if (Guid.TryParse(raw, out var guid))
        {
            tenantId = new TenantId(guid);
            return true;
        }
        tenantId = default;
        return false;
    }

    private static string? HashRemoteIp(HttpContext http)
    {
        var remote = http.Connection.RemoteIpAddress?.ToString();
        if (string.IsNullOrEmpty(remote)) return null;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(remote));
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public sealed record RequestLinkBody(string Email);
    public sealed record SelectTenantBody(Guid TenantId);
    public sealed record InviteBody(string Email, string? Role);

    public sealed record SessionAccountDto(Guid AccountId, string Email, string? DisplayName);
    public sealed record SessionTenantDto(Guid TenantId, string DisplayName, string Status, string Role);
    public sealed record SessionMeResponse(
        SessionAccountDto Account,
        Guid? CurrentTenantId,
        IReadOnlyList<SessionTenantDto> Tenants);
}
