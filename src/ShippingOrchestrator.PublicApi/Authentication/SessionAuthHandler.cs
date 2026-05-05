using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Application.Identity;

namespace ShippingOrchestrator.PublicApi.Authentication;

public sealed class SessionAuthOptions : AuthenticationSchemeOptions { }

/// <summary>
/// Cookie-backed session authentication. The cookie carries an opaque base64url secret;
/// only its SHA-256 hash is persisted in <c>orchestrator.auth_sessions.session_hash</c>, so a
/// DB dump cannot impersonate users. On every authenticated request the handler:
/// <list type="number">
///   <item>Reads the cookie value (the raw session secret).</item>
///   <item>Hashes it and looks up the session row.</item>
///   <item>Verifies the row is active (not revoked, not expired).</item>
///   <item>Touches <c>LastSeenAt</c> and slides expiry.</item>
///   <item>Builds claims: <c>session_id</c>, <c>account_id</c>, and (if a tenant has been
///         picked) <c>tenant_id</c>.</item>
/// </list>
/// </summary>
public sealed class SessionAuthHandler : AuthenticationHandler<SessionAuthOptions>
{
    public const string SchemeName = "Session";

    public const string AccountIdClaim = "account_id";
    public const string TenantIdClaim = "tenant_id";
    public const string SessionIdClaim = "session_id";

    public SessionAuthHandler(
        IOptionsMonitor<SessionAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authOptions = Context.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value;
        if (!Request.Cookies.TryGetValue(authOptions.CookieName, out var rawSecret) || string.IsNullOrEmpty(rawSecret))
            return AuthenticateResult.NoResult();

        string sessionHash;
        try
        {
            sessionHash = TokenGenerator.Hash(rawSecret);
        }
        catch
        {
            return AuthenticateResult.Fail("Invalid session cookie format.");
        }

        var sessions = Context.RequestServices.GetRequiredService<IAuthSessionRepository>();
        var clock = Context.RequestServices.GetRequiredService<IClock>();
        var unitOfWork = Context.RequestServices.GetRequiredService<IUnitOfWork>();

        var session = await sessions.FindByHashAsync(sessionHash, Context.RequestAborted).ConfigureAwait(false);
        if (session is null) return AuthenticateResult.Fail("Unknown session.");

        var now = clock.UtcNow;
        if (!session.IsActive(now)) return AuthenticateResult.Fail("Session expired or revoked.");

        // Slide on every request. The DB write is an UPDATE on the indexed PK, fast enough
        // for the auth path; cheaper than running a separate maintenance job.
        session.Touch(now, TimeSpan.FromSeconds(authOptions.SessionTtlSeconds));
        await unitOfWork.SaveChangesAsync(Context.RequestAborted).ConfigureAwait(false);

        var claims = new List<Claim>
        {
            new(SessionIdClaim, session.Id.ToString()),
            new(AccountIdClaim, session.AccountId.Value.ToString()),
            new(ClaimTypes.NameIdentifier, session.AccountId.Value.ToString()),
        };

        if (session.CurrentTenantId is { } tenantId)
            claims.Add(new Claim(TenantIdClaim, tenantId.Value.ToString()));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return AuthenticateResult.Success(ticket);
    }

    /// <summary>
    /// Builds the cookie attributes used when writing the session cookie. Production gets the
    /// <c>__Host-</c> prefix (forces Secure + Path=/ + no Domain). Development drops the
    /// prefix so it works on plain HTTP localhost.
    /// </summary>
    public static CookieOptions BuildCookieOptions(IHostEnvironment env, AuthOptions options, DateTimeOffset expiresAt)
    {
        var prod = env.IsProduction();
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = prod,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = expiresAt,
            IsEssential = true,
        };
    }

    public static string ResolveCookieName(IHostEnvironment env, AuthOptions options) =>
        env.IsProduction() && !options.CookieName.StartsWith("__Host-", StringComparison.Ordinal)
            ? "__Host-" + options.CookieName
            : options.CookieName;
}
