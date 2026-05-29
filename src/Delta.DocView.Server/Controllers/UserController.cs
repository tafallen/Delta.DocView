using System.Security.Claims;
using Delta.DocView.Server.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Delta.DocView.Server.Controllers;

[ApiController]
public sealed class UserController : ControllerBase
{
    /// <summary>Returns the current user's display name and initials.</summary>
    [HttpGet("/api/user")]
    [Authorize]
    public IActionResult GetUser()
    {
        var name = User.Identity?.Name
                   ?? User.FindFirstValue("name")
                   ?? User.FindFirstValue("preferred_username")
                   ?? AuthConstants.DefaultDevUser;

        return Ok(new
        {
            name,
            initials = Initials(name),
            authenticated = User.Identity?.IsAuthenticated ?? false
        });
    }

    /// <summary>
    /// Clears the session and redirects to /. In bypass mode the cookie/OIDC
    /// schemes may not exist — SignOut is best-effort; redirect is always issued.
    /// </summary>
    [HttpGet("/logout")]
    public IActionResult Logout()
    {
        var authType = User.Identity?.AuthenticationType;
        if (authType == AuthConstants.DevBypassScheme)
        {
            // Dev bypass: no real session to clear, just redirect.
            return Redirect("/");
        }

        return SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    private static string Initials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Trim().Split(' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 1
            ? parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant()
            : string.Concat(parts[0][0], parts[^1][0]).ToUpperInvariant();
    }
}
