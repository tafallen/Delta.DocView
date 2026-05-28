using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Delta.DocView.Tests.Components;

public class PaletteJSInteropTests
{
    private const string FocusElement = "docview.focus.element";
    private const string RestorePrevious = "docview.focus.restorePrevious";
    private const string ScrollIntoView = "docview.scrollIntoViewIfNeeded";

    private static Step MakeStep(string id, string pattern, int used) => new()
    {
        Id = id,
        Type = "Given",
        Pattern = pattern,
        Params = Array.Empty<StepParam>(),
        File = "Auth.cs",
        Line = 1,
        Domain = "auth",
        Tags = Array.Empty<string>(),
        Used = used,
        Description = "",
        Source = "",
        SuggestsNext = Array.Empty<string>(),
    };

    private static StepLibrary BuildLibrary(int n = 5)
    {
        var steps = new List<Step>();
        for (var i = 0; i < n; i++)
        {
            steps.Add(MakeStep($"s{i}", $"pattern number {i}", n - i));
        }
        return new StepLibrary
        {
            Steps = steps,
            Domains = new List<StepDomain>
            {
                new() { Id = "auth", Label = "Auth" },
            },
        };
    }

    private static (TestContext ctx, PaletteState palette, IKeyboardActions actions) NewContext()
    {
        var ctx = new TestContext();
        var store = new ClientStepLibraryStore();
        store.Populate(BuildLibrary());

        ctx.Services.AddScoped(_ => store);
        ctx.Services.AddScoped<FilterState>();
        ctx.Services.AddScoped<IFavouritesStore, InMemoryFavouritesStore>();
        ctx.Services.AddScoped<SelectionState>();
        ctx.Services.AddScoped<FilteredStepsProvider>();
        ctx.Services.AddScoped<IKeyboardActions, KeyboardActions>();
        ctx.Services.AddScoped<PaletteState>();
        ctx.JSInterop.Mode = JSRuntimeMode.Strict;
        // Pre-register all JS calls Palette makes. SetVoidResult() ensures the
        // returned task completes synchronously, so the `await JS.InvokeVoidAsync(...)`
        // calls in OnAfterRenderAsync resume in the same dispatcher continuation
        // — this is what keeps `_wasOpen` and `_lastScrolledIndex` in lock-step
        // with renders. Without SetVoidResult the await would hang and the
        // trailing field assignments would never run.
        ctx.JSInterop.SetupVoid(FocusElement).SetVoidResult();
        ctx.JSInterop.SetupVoid(RestorePrevious).SetVoidResult();
        ctx.JSInterop.SetupVoid(ScrollIntoView).SetVoidResult();

        var palette = ctx.Services.GetRequiredService<PaletteState>();
        var actions = ctx.Services.GetRequiredService<IKeyboardActions>();
        return (ctx, palette, actions);
    }

    [Fact]
    public void Opening_Palette_Calls_FocusElement_With_InputId()
    {
        var (ctx, _, actions) = NewContext();

        var cut = ctx.RenderComponent<Palette>();
        actions.OpenPalette();
        cut.WaitForAssertion(() => Assert.Single(ctx.JSInterop.Invocations[FocusElement]));

        var invocation = ctx.JSInterop.Invocations[FocusElement].Single();
        var inputId = Assert.IsType<string>(invocation.Arguments[0]);
        Assert.StartsWith("palette-input-", inputId);
    }

    [Fact]
    public void Closing_Palette_Calls_RestorePrevious()
    {
        var (ctx, _, actions) = NewContext();

        var cut = ctx.RenderComponent<Palette>();
        actions.OpenPalette();
        cut.WaitForAssertion(() => Assert.NotEmpty(ctx.JSInterop.Invocations[FocusElement]));
        actions.CloseOverlay();

        cut.WaitForAssertion(() =>
            Assert.NotEmpty(ctx.JSInterop.Invocations[RestorePrevious]));
    }

    [Fact]
    public void Selection_Index_Change_Fires_ScrollIntoView_Once_Per_Change()
    {
        var (ctx, palette, actions) = NewContext();

        var cut = ctx.RenderComponent<Palette>();
        actions.OpenPalette();
        cut.WaitForAssertion(() => Assert.NotEmpty(ctx.JSInterop.Invocations[ScrollIntoView]));

        var n0 = ctx.JSInterop.Invocations[ScrollIntoView].Count;
        palette.MoveSelectionDown();

        cut.WaitForAssertion(() =>
            Assert.Equal(n0 + 1, ctx.JSInterop.Invocations[ScrollIntoView].Count));
        var selector = Assert.IsType<string>(
            ctx.JSInterop.Invocations[ScrollIntoView].Last().Arguments[0]);
        Assert.Contains("data-result-index='1'", selector);
    }

    [Fact]
    public void Repeated_Render_With_Same_SelectedIndex_Does_Not_Fire_Scroll()
    {
        var (ctx, palette, actions) = NewContext();

        var cut = ctx.RenderComponent<Palette>();
        actions.OpenPalette();
        cut.WaitForAssertion(() => Assert.NotEmpty(ctx.JSInterop.Invocations[ScrollIntoView]));

        var baseline = ctx.JSInterop.Invocations[ScrollIntoView].Count;

        // No-op: same index. Then force a re-render.
        palette.SetSelectedIndex(palette.SelectedIndex);
        cut.Render();

        Assert.Equal(baseline, ctx.JSInterop.Invocations[ScrollIntoView].Count);
    }

    [Fact]
    public void Closing_Then_Reopening_Re_Fires_Focus_Call()
    {
        var (ctx, _, actions) = NewContext();

        var cut = ctx.RenderComponent<Palette>();
        actions.OpenPalette();
        cut.WaitForAssertion(() => Assert.Single(ctx.JSInterop.Invocations[FocusElement]));
        actions.CloseOverlay();
        cut.WaitForAssertion(() => Assert.NotEmpty(ctx.JSInterop.Invocations[RestorePrevious]));
        actions.OpenPalette();

        cut.WaitForAssertion(
            () => Assert.Equal(2, ctx.JSInterop.Invocations[FocusElement].Count),
            TimeSpan.FromSeconds(5));
    }
}
