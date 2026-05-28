using Bunit;
using Delta.DocView.Client.Layout;
using Microsoft.Extensions.DependencyInjection;
using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;

namespace Delta.DocView.Tests.Components;

public class HeaderTests : TestContext
{
    private static ClientStepLibraryStore MakeStore(int stepCount = 1, string version = "1.0.0")
    {
        var steps = Enumerable.Range(0, stepCount)
            .Select(i => new Step { Id = $"s{i}", Type = "Given", Pattern = $"step {i}", Domain = "Auth" })
            .ToList<Step>();
        var library = new StepLibrary
        {
            Version = version,
            GeneratedAt = "2026-01-01T00:00:00Z",
            Domains = [new StepDomain { Id = "Auth", Label = "Auth & Identity" }],
            Steps = steps,
            Signature = new StepSignature { Algorithm = "SHA-256", Digest = new string('0', 64) }
        };
        var store = new ClientStepLibraryStore();
        store.Populate(library);
        return store;
    }

    private void Register(ClientStepLibraryStore store)
    {
        Services.AddSingleton(store);
        Services.AddSingleton<FilterState>();
    }

    [Fact]
    public void Header_RendersTitle()
    {
        Register(MakeStore());

        var cut = RenderComponent<Header>();

        Assert.Contains("Delta · Step Library", cut.Markup);
    }

    [Fact]
    public void Header_RendersStepCount()
    {
        Register(MakeStore(stepCount: 42));

        var cut = RenderComponent<Header>();

        Assert.Contains("42", cut.Markup);
    }

    [Fact]
    public void Header_RendersVersion()
    {
        Register(MakeStore(version: "3.1.0"));

        var cut = RenderComponent<Header>();

        Assert.Contains("3.1.0", cut.Markup);
    }

    [Fact]
    public void Header_DarkToggle_CallsSetDark()
    {
        Register(MakeStore());
        var jsInvoke = JSInterop.SetupVoid("docview.setDark", _ => true);

        var cut = RenderComponent<Header>();
        cut.Find("[data-testid='dark-toggle']").Click();

        jsInvoke.VerifyInvoke("docview.setDark", 1);
    }

    [Fact]
    public void Header_RendersSearchInput()
    {
        Register(MakeStore());

        var cut = RenderComponent<Header>();

        Assert.NotNull(cut.Find("input[type='search']"));
    }

    [Fact]
    public void Header_RendersAvatarChip()
    {
        Register(MakeStore());

        var cut = RenderComponent<Header>();

        Assert.Contains("avatar-chip", cut.Markup);
    }

    [Fact]
    public void Header_SearchInput_UpdatesFilterStateQuery()
    {
        Register(MakeStore());
        var state = Services.GetRequiredService<FilterState>();

        var cut = RenderComponent<Header>();
        cut.Find("input.search-input").Input("hello");

        Assert.Equal("hello", state.Query);
    }

    [Fact]
    public void Header_SearchInput_ReflectsExternalQueryWrite()
    {
        Register(MakeStore());
        var state = Services.GetRequiredService<FilterState>();

        var cut = RenderComponent<Header>();
        state.SetQuery("external");

        Assert.Equal("external", cut.Find("input.search-input").GetAttribute("value"));
    }
}
