using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Delta.DocView.Server.Auth;

public sealed class DevBypassAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration config)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var name = config[AuthConstants.EnvDevUser] ?? AuthConstants.DefaultDevUser;
        var claims = new[] { new Claim(ClaimTypes.Name, name) };
        var identity = new ClaimsIdentity(claims, AuthConstants.DevBypassScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AuthConstants.DevBypassScheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
