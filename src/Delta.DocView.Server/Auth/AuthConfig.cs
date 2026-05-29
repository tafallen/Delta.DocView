using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;

namespace Delta.DocView.Server.Auth;

public static class AuthConfig
{
    /// <summary>
    /// Registers either the dev-bypass auth handler (Development env or
    /// DOCVIEW_AUTH_DISABLED=true) or Entra ID OIDC (all other environments).
    /// In both cases adds a default authorization policy requiring the user
    /// to be authenticated — so all routes are protected unless explicitly
    /// marked [AllowAnonymous].
    /// </summary>
    public static void AddDocViewAuth(this WebApplicationBuilder builder)
    {
        var env = builder.Environment;
        var config = builder.Configuration;
        bool bypass = env.IsDevelopment()
            || string.Equals(config[AuthConstants.EnvAuthDisabled], "true",
                             StringComparison.OrdinalIgnoreCase);

        if (bypass)
        {
            builder.Services
                .AddAuthentication(AuthConstants.DevBypassScheme)
                .AddScheme<AuthenticationSchemeOptions, DevBypassAuthHandler>(
                    AuthConstants.DevBypassScheme, _ => { });
        }
        else
        {
            builder.Services
                .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApp(config.GetSection("AzureAd"));
        }

        builder.Services.AddAuthorization(opts =>
            opts.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());
    }
}
