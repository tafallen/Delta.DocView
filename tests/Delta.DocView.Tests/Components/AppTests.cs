using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Delta.DocView.Client.Services;
using Delta.DocView.Shared;
using Delta.DocView.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Delta.DocView.Tests.Components;

public class AppTests
{
    private static readonly StepLibrary SampleLibrary = new()
    {
        Version = "1.0.0",
        GeneratedAt = "2026-01-01T00:00:00Z",
        GeneratorVersion = "1.0.0",
        Domains = [new StepDomain { Id = "Auth", Label = "Auth & Identity" }],
        Steps =
        [
            new Step
            {
                Id = "auth-001a2b3c", Type = "Given",
                Pattern = "I am logged in as {string}",
                Params = [], File = "Auth/AuthSteps.cs", Line = 10,
                Domain = "Auth", Tags = [], Used = 1,
                Description = "", Source = "", SuggestsNext = []
            }
        ],
        Signature = new StepSignature { Algorithm = "SHA-256", Digest = new string('0', 64) }
    };

    [Fact]
    public void Renders_LoadingScreen_WhileLoadInFlight()
    {
        using var ctx = new TestContext();
        var pending = new TaskCompletionSource<HttpResponseMessage>();
        var http = new HttpClient(new StubHandler(_ => pending.Task))
        {
            BaseAddress = new Uri("http://localhost/")
        };
        ctx.Services.AddScoped(_ => http);
        ctx.Services.AddScoped<ClientStepLibraryStore>();
        ctx.Services.AddScoped<LibraryApiClient>();
        ctx.Services.AddScoped<FilterState>();
        ctx.Services.AddScoped<IFavouritesStore, InMemoryFavouritesStore>();
        ctx.Services.AddScoped<SelectionState>();
        ctx.Services.AddScoped<FilteredStepsProvider>();
        ctx.Services.AddScoped<IKeyboardActions, KeyboardActions>();
        ctx.Services.AddScoped<PaletteState>();
        ctx.Services.AddScoped<ComposerState>();
        ctx.Services.AddScoped(_ => Substitute.For<IPlatform>());
        ctx.Services.AddScoped<ShortcutsState>();
        ctx.Services.AddScoped<TweaksStore>();
        ctx.Services.AddScoped<TweaksPanelState>();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.RenderComponent<Delta.DocView.Client.App>();

        Assert.Contains("loading-screen", cut.Markup);
        pending.SetResult(new HttpResponseMessage(HttpStatusCode.OK));
    }

    [Fact]
    public void Renders_StartupErrorPage_WhenLoadFails()
    {
        using var ctx = ContextWithResponse(HttpStatusCode.ServiceUnavailable,
            new { error = "Library file not found." });
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.RenderComponent<Delta.DocView.Client.App>();

        Assert.Contains("startup-error", cut.Markup);
        Assert.Contains("Library file not found.", cut.Markup);
    }

    [Fact]
    public void Renders_MainLayout_WhenLoadSucceeds()
    {
        using var ctx = ContextWithResponse(HttpStatusCode.OK,
            new LibraryResponse(SampleLibrary, null));
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.RenderComponent<Delta.DocView.Client.App>();

        Assert.Contains("app-shell", cut.Markup);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static TestContext ContextWithResponse(HttpStatusCode status, object body)
    {
        var json = JsonSerializer.Serialize(body);
        var ctx = new TestContext();
        var http = new HttpClient(new StubHandler(_ => Task.FromResult(
            new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            })))
        {
            BaseAddress = new Uri("http://localhost/")
        };
        ctx.Services.AddScoped(_ => http);
        ctx.Services.AddScoped<ClientStepLibraryStore>();
        ctx.Services.AddScoped<LibraryApiClient>();
        ctx.Services.AddScoped<FilterState>();
        ctx.Services.AddScoped<IFavouritesStore, InMemoryFavouritesStore>();
        ctx.Services.AddScoped<SelectionState>();
        ctx.Services.AddScoped<FilteredStepsProvider>();
        ctx.Services.AddScoped<IKeyboardActions, KeyboardActions>();
        ctx.Services.AddScoped<PaletteState>();
        ctx.Services.AddScoped<ComposerState>();
        ctx.Services.AddScoped(_ => Substitute.For<IPlatform>());
        ctx.Services.AddScoped<ShortcutsState>();
        ctx.Services.AddScoped<TweaksStore>();
        ctx.Services.AddScoped<TweaksPanelState>();
        return ctx;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _fn;
        public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> fn) => _fn = fn;
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => _fn(request);
    }
}
