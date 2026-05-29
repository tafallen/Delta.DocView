using Bunit;
using Delta.DocView.Client.Layout;
using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Delta.DocView.Tests.Components;

public class MainLayoutTests : TestContext
{
    private static ClientStepLibraryStore MakeStore()
    {
        var store = new ClientStepLibraryStore();
        store.Populate(new StepLibrary
        {
            Version = "1.0.0",
            GeneratedAt = "2026-01-01T00:00:00Z",
            Domains = [],
            Steps = [],
            Signature = new StepSignature { Algorithm = "SHA-256", Digest = new string('0', 64) }
        });
        return store;
    }

    private void RegisterServices()
    {
        Services.AddSingleton(MakeStore());
        Services.AddSingleton<FilterState>();
        Services.AddSingleton<IFavouritesStore, InMemoryFavouritesStore>();
        Services.AddSingleton<SelectionState>();
        Services.AddSingleton<FilteredStepsProvider>();
        Services.AddSingleton<IKeyboardActions, KeyboardActions>();
        Services.AddSingleton<PaletteState>();
        Services.AddSingleton<ComposerState>();
        Services.AddSingleton(_ => Substitute.For<IPlatform>());
        Services.AddSingleton<ShortcutsState>();
        Services.AddSingleton<TweaksStore>();
        Services.AddSingleton<TweaksPanelState>();
        Services.AddSingleton(_ => new UserClient(
            new System.Net.Http.HttpClient(new FallbackUserHandler())
            { BaseAddress = new Uri("http://localhost/") }));
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private sealed class FallbackUserHandler : System.Net.Http.HttpMessageHandler
    {
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(
                    """{"name":"QA","initials":"QA","authenticated":false}""",
                    System.Text.Encoding.UTF8, "application/json")
            });
    }

    [Fact]
    public void MainLayout_RendersLeftRail()
    {
        RegisterServices();
        var cut = RenderComponent<MainLayout>();
        Assert.NotNull(cut.Find(".left-rail"));
    }

    [Fact]
    public void MainLayout_RendersStepListPanel()
    {
        RegisterServices();
        var cut = RenderComponent<MainLayout>();
        Assert.NotNull(cut.Find(".step-list-panel"));
    }

    [Fact]
    public void MainLayout_RendersDetailPanel()
    {
        RegisterServices();
        var cut = RenderComponent<MainLayout>();
        Assert.NotNull(cut.Find(".detail-panel"));
    }

    [Fact]
    public void MainLayout_ShowsWarningBanner_WhenWarningProvided()
    {
        RegisterServices();
        var cut = RenderComponent<MainLayout>(p =>
            p.Add(c => c.Warning, "Signature mismatch — library may have been modified."));
        Assert.Contains("warning-banner", cut.Markup);
        Assert.Contains("Signature mismatch", cut.Markup);
    }

    [Fact]
    public void MainLayout_HidesWarningBanner_WhenNoWarning()
    {
        RegisterServices();
        var cut = RenderComponent<MainLayout>(p =>
            p.Add(c => c.Warning, (string?)null));
        Assert.DoesNotContain("warning-banner", cut.Markup);
    }
}
