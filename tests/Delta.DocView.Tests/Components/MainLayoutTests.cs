using Bunit;
using Delta.DocView.Client.Layout;
using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

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
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid("docview.setDark", _ => true);
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
