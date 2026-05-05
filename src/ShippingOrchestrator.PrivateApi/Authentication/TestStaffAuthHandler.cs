using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ShippingOrchestrator.PrivateApi.Authentication;

/// <summary>
/// Dev/test scheme for the internal API: trusts the <c>X-Staff-Role</c> + <c>X-Staff-User</c>
/// headers. Production wires the staff Cognito user pool's JWT bearer instead.
/// </summary>
public sealed class TestStaffAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestStaffHeader";

    public TestStaffAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Staff-Role", out var roleValues) || roleValues.Count == 0)
            return Task.FromResult(AuthenticateResult.NoResult());

        var role = roleValues.ToString();
        var user = Request.Headers.TryGetValue("X-Staff-User", out var u) ? u.ToString() : "staff";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user),
            new Claim(ClaimTypes.Role, role),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
