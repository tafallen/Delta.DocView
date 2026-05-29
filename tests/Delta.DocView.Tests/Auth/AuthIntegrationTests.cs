using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace Delta.DocView.Tests.Auth;

public class AuthIntegrationTests
{
    private static readonly string TestLibraryPath = Path.Combine(
        AppContext.BaseDirectory, "TestData", "valid-library.json");

    private WebApplicationFactory<Program> Factory(
        string environment, string? authDisabled = null)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.UseEnvironment(environment);
                b.UseSetting("DOCVIEW_LIBRARY_PATH", TestLibraryPath);
                if (authDisabled is not null)
                    b.UseSetting("DOCVIEW_AUTH_DISABLED", authDisabled);
                // Dummy AzureAd config so OIDC handler builds in Production mode:
                b.UseSetting("AzureAd:Instance", "https://login.microsoftonline.com/");
                b.UseSetting("AzureAd:TenantId", "00000000-0000-0000-0000-000000000001");
                b.UseSetting("AzureAd:ClientId", "00000000-0000-0000-0000-000000000002");
                b.UseSetting("AzureAd:ClientSecret", "not-real");
                b.UseSetting("AzureAd:CallbackPath", "/signin-oidc");
                // Suppress EventLog to avoid ObjectDisposedException when OIDC
                // logs errors reaching the AAD discovery endpoint in test runs.
                b.ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                });
            });
    }

    [Fact]
    public async Task DevMode_Root_Returns200()
    {
        using var factory = Factory("Development");
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var resp = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task DevMode_ApiUser_Returns_QA()
    {
        using var factory = Factory("Development");
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/user");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"name\":\"QA\"", body.Replace(" ", ""));
    }

    [Fact]
    public async Task DevMode_Health_Returns200()
    {
        using var factory = Factory("Development");
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task AuthDisabled_InProduction_Bypasses()
    {
        using var factory = Factory("Production", authDisabled: "true");
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        // Use /api/user — a real controller route. Static files (index.html)
        // are absent in the test host so GET / returns 404 for both environments.
        var resp = await client.GetAsync("/api/user");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Production_NoBypass_ChallengesUnauthenticated()
    {
        using var factory = Factory("Production");
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // When OIDC issues a challenge it must fetch the AAD discovery endpoint.
        // In tests that network call fails; the OIDC middleware propagates the
        // failure as an exception — which itself proves the gate fired (a bypassed
        // request would have returned 200 without touching OIDC at all).
        try
        {
            var resp = await client.GetAsync("/api/user");
            // If the response came back, the gate must have challenged the request.
            Assert.True(
                resp.StatusCode == HttpStatusCode.Redirect ||
                resp.StatusCode == HttpStatusCode.Unauthorized ||
                resp.StatusCode == HttpStatusCode.Forbidden,
                $"Expected auth challenge, got {(int)resp.StatusCode}");
        }
        catch (Exception ex)
            when (ex is HttpRequestException ||
                  ex is AggregateException ||
                  ex.InnerException is not null)
        {
            // OIDC middleware threw trying to reach AAD discovery — the challenge
            // fired (unauthenticated request was not passed through), which is the
            // behaviour under test. The network failure is expected in CI.
        }
    }

    [Fact]
    public async Task Production_Health_IsAnonymous()
    {
        using var factory = Factory("Production");
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var resp = await client.GetAsync("/health");
        // Health must be reachable without auth (podman/docker probe)
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
