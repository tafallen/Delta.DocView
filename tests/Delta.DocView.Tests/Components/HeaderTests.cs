using Bunit;
using Delta.DocView.Client.Layout;
using Microsoft.Extensions.DependencyInjection;
using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;
using NSubstitute;

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

    private void Register(ClientStepLibraryStore store, IPlatform? platform = null, IKeyboardActions? actions = null, bool prefersDark = false)
    {
        Services.AddSingleton(store);
        Services.AddSingleton<FilterState>();
        Services.AddSingleton(_ => platform ?? Substitute.For<IPlatform>());
        Services.AddSingleton(_ => actions ?? Substitute.For<IKeyboardActions>());
        JSInterop.Setup<bool>("docview.prefersDark").SetResult(prefersDark);
        JSInterop.SetupVoid("docview.setDark", _ => true);
    }

    [Fact]
    public void Header_RendersTitle()
    {
        Register(MakeStore());

        var cut = RenderComponent<Header>();

        Assert.Contains("Triangle · Step Library", cut.Markup);
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

        var cut = RenderComponent<Header>();
        cut.Find("[data-testid='dark-toggle']").Click();

        // once from init (OS preference), once from the toggle click
        JSInterop.VerifyInvoke("docview.setDark", 2);
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

        cut.WaitForAssertion(() => Assert.Equal("hello", state.Query), timeout: TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void Header_SearchInput_DebouncesRapidInput()
    {
        Register(MakeStore());
        var state = Services.GetRequiredService<FilterState>();
        var changedCount = 0;
        state.Changed += () => Interlocked.Increment(ref changedCount);

        var cut = RenderComponent<Header>();
        var input = cut.Find("input.search-input");
        input.Input("a");
        input.Input("ab");
        input.Input("abc");

        cut.WaitForAssertion(() => Assert.Equal("abc", state.Query), timeout: TimeSpan.FromMilliseconds(500));
        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void Quick_Find_Button_Shows_Command_K_On_Mac()
    {
        var platform = Substitute.For<IPlatform>();
        platform.IsMac.Returns(true);
        platform.ShortcutLabel("K").Returns("⌘K");
        Register(MakeStore(), platform: platform);

        var cut = RenderComponent<Header>();

        Assert.Equal("⌘K", cut.Find("[data-testid='quick-find']").TextContent.Trim());
    }

    [Fact]
    public void Quick_Find_Button_Shows_Ctrl_K_On_NonMac()
    {
        var platform = Substitute.For<IPlatform>();
        platform.IsMac.Returns(false);
        platform.ShortcutLabel("K").Returns("Ctrl+K");
        Register(MakeStore(), platform: platform);

        var cut = RenderComponent<Header>();

        Assert.Equal("Ctrl+K", cut.Find("[data-testid='quick-find']").TextContent.Trim());
    }

    [Fact]
    public void Quick_Find_Click_Calls_OpenPalette()
    {
        var actions = Substitute.For<IKeyboardActions>();
        Register(MakeStore(), actions: actions);

        var cut = RenderComponent<Header>();
        cut.Find("[data-testid='quick-find']").Click();

        actions.Received(1).OpenPalette();
    }

    [Fact]
    public void Quick_Find_Button_Has_Aria_Label()
    {
        var platform = Substitute.For<IPlatform>();
        platform.IsMac.Returns(true);
        platform.ShortcutLabel("K").Returns("⌘K");
        Register(MakeStore(), platform: platform);

        var cut = RenderComponent<Header>();

        Assert.Equal("Quick find (⌘K)", cut.Find("[data-testid='quick-find']").GetAttribute("aria-label"));
    }

    [Fact]
    public void Header_Init_AppliesDark_WhenOsPrefersDark()
    {
        Register(MakeStore(), prefersDark: true);

        RenderComponent<Header>();

        var calls = JSInterop.Invocations["docview.setDark"];
        Assert.True(calls.Count >= 1);
        Assert.Equal(true, calls[0].Arguments[0]);
    }

    [Fact]
    public void Header_Init_AppliesLight_WhenOsDoesNotPreferDark()
    {
        Register(MakeStore(), prefersDark: false);

        RenderComponent<Header>();

        var calls = JSInterop.Invocations["docview.setDark"];
        Assert.True(calls.Count >= 1);
        Assert.Equal(false, calls[0].Arguments[0]);
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
