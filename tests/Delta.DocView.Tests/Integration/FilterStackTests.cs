using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Client.Layout;
using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Delta.DocView.Tests.Integration;

/// <summary>
/// Integration coverage for the filter rail and header search driving <see cref="FilterState"/>,
/// with <see cref="FilterEngine.Apply"/> verifying the projected set on every interaction.
/// </summary>
public class FilterStackTests
{
    private static StepLibrary BuildLibrary() => new()
    {
        Version = "1.0.0",
        GeneratedAt = "2026-01-01T00:00:00Z",
        Domains =
        [
            new StepDomain { Id = "Auth", Label = "Auth & Identity" },
            new StepDomain { Id = "Billing", Label = "Billing" }
        ],
        Steps =
        [
            new Step
            {
                Id = "g1",
                Type = "Given",
                Domain = "Auth",
                Pattern = "I am logged in as {string}",
                Params = [new StepParam { Name = "user", Type = "string" }],
                SuggestsNext = ["g2"],
                Used = 5
            },
            new Step
            {
                Id = "g2",
                Type = "Given",
                Domain = "Auth",
                Pattern = "I have {int} active sessions",
                Params = [new StepParam { Name = "count", Type = "int" }],
                Used = 3
            },
            new Step
            {
                Id = "g3",
                Type = "When",
                Domain = "Billing",
                Pattern = "I add card ending {string}",
                Params = [new StepParam { Name = "last4", Type = "string" }],
                Used = 99
            },
            new Step
            {
                Id = "g4",
                Type = "When",
                Domain = "Billing",
                Pattern = "I post a payment of {decimal}",
                Params = [new StepParam { Name = "amount", Type = "decimal" }],
                Used = 7
            },
            new Step
            {
                Id = "g5",
                Type = "Then",
                Domain = "Auth",
                Pattern = "I see the dashboard",
                Params = [],
                Used = 2
            },
            new Step
            {
                Id = "g6",
                Type = "Then",
                Domain = "Billing",
                Pattern = "I receive a receipt for {string}",
                Params = [new StepParam { Name = "ref", Type = "string" }],
                Used = 1
            }
        ],
        Signature = new StepSignature { Algorithm = "SHA-256", Digest = new string('0', 64) }
    };

    private static (TestContext ctx, ClientStepLibraryStore store, FilterState state, IFavouritesStore favs) NewContext()
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
        ctx.Services.AddScoped<ComposerState>();
        ctx.Services.AddScoped<PaletteState>();
        ctx.Services.AddScoped(_ => Substitute.For<IPlatform>());
        ctx.Services.AddScoped<ShortcutsState>();
        ctx.Services.AddScoped<TweaksStore>();
        ctx.Services.AddScoped<TweaksPanelState>();
        ctx.Services.AddScoped(_ => new Delta.DocView.Client.Services.UserClient(
            new System.Net.Http.HttpClient(new FallbackUserHandler())
            { BaseAddress = new Uri("http://localhost/") }));
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var state = ctx.Services.GetRequiredService<FilterState>();
        var favs = ctx.Services.GetRequiredService<IFavouritesStore>();
        return (ctx, store, state, favs);
    }

    [Fact]
    public void TypeFilter_Click_NarrowsResults()
    {
        var (ctx, store, state, favs) = NewContext();
        using var _ = ctx;
        ctx.RenderComponent<Header>();
        var rail = ctx.RenderComponent<LeftRail>();

        Assert.Equal(6, FilterEngine.Apply(store.Steps, state, favs).Count);

        rail.Find("button.step-type[data-type='When']").Click();

        var projected = FilterEngine.Apply(store.Steps, state, favs);
        Assert.Equal(4, projected.Count);
        Assert.DoesNotContain(projected, s => s.Type == "When");
    }

    [Fact]
    public void DomainFilter_Click_NarrowsToDomain()
    {
        var (ctx, store, state, favs) = NewContext();
        using var _ = ctx;
        ctx.RenderComponent<Header>();
        var rail = ctx.RenderComponent<LeftRail>();

        rail.Find("button.domain-row[data-domain='Billing']").Click();

        var projected = FilterEngine.Apply(store.Steps, state, favs);
        Assert.Equal(3, projected.Count);
        Assert.All(projected, s => Assert.Equal("Billing", s.Domain));
    }

    [Fact]
    public void ParamTypeFilter_Click_NarrowsToParamType()
    {
        var (ctx, store, state, favs) = NewContext();
        using var _ = ctx;
        ctx.RenderComponent<Header>();
        var rail = ctx.RenderComponent<LeftRail>();

        rail.Find("button.param-chip[data-paramtype='decimal']").Click();

        var projected = FilterEngine.Apply(store.Steps, state, favs);
        Assert.Single(projected);
        Assert.Equal("g4", projected[0].Id);
    }

    [Fact]
    public void SearchQuery_AfterDebounce_NarrowsResults()
    {
        var (ctx, store, state, favs) = NewContext();
        using var _ = ctx;
        var header = ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();

        header.Find("input.search-input").Input("payment");

        header.WaitForAssertion(
            () => Assert.Single(FilterEngine.Apply(store.Steps, state, favs)),
            timeout: TimeSpan.FromMilliseconds(500));

        var projected = FilterEngine.Apply(store.Steps, state, favs);
        Assert.Equal("g4", projected[0].Id);
    }

    [Fact]
    public void FavouritesOnly_Toggle_NarrowsToFavourites()
    {
        var (ctx, store, state, favs) = NewContext();
        using var _ = ctx;
        ctx.RenderComponent<Header>();
        var rail = ctx.RenderComponent<LeftRail>();

        favs.Toggle("g3");
        rail.Find("[data-testid='favourites-toggle']").Click();

        var projected = FilterEngine.Apply(store.Steps, state, favs);
        Assert.Single(projected);
        Assert.Equal("g3", projected[0].Id);
    }

    [Fact]
    public void StepList_TypeClick_NarrowsRenderedRows()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        ctx.RenderComponent<Header>();
        var rail = ctx.RenderComponent<LeftRail>();
        var list = ctx.RenderComponent<StepList>();

        Assert.Equal(6, list.FindAll(".step-row").Count);

        rail.Find("button.step-type[data-type='When']").Click();

        list.WaitForAssertion(
            () => Assert.Equal(4, list.FindAll(".step-row").Count),
            timeout: TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void StepList_DomainClick_HidesGroupHeaders()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        ctx.RenderComponent<Header>();
        var rail = ctx.RenderComponent<LeftRail>();
        var list = ctx.RenderComponent<StepList>();

        Assert.NotEmpty(list.FindAll(".step-group-header"));

        rail.Find("button.domain-row[data-domain='Auth']").Click();

        list.WaitForAssertion(
            () => Assert.Empty(list.FindAll(".step-group-header")),
            timeout: TimeSpan.FromMilliseconds(500));
        Assert.Equal(3, list.FindAll(".step-row").Count);
    }

    [Fact]
    public void StepList_NoMatchQuery_RendersEmptyState()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        var header = ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        var list = ctx.RenderComponent<StepList>();

        header.Find("input.search-input").Input("xyzzy");

        list.WaitForAssertion(
            () =>
            {
                Assert.NotNull(list.Find(".step-list-empty"));
                Assert.Empty(list.FindAll(".step-row"));
            },
            timeout: TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void StepList_MatchingQuery_HighlightsMatchesWithMark()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        var header = ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        var list = ctx.RenderComponent<StepList>();

        header.Find("input.search-input").Input("logged");

        list.WaitForAssertion(
            () =>
            {
                var marks = list.FindAll(".step-pattern mark");
                Assert.NotEmpty(marks);
            },
            timeout: TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void StepList_RowClick_SelectsStep()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        var list = ctx.RenderComponent<StepList>();
        var selection = ctx.Services.GetRequiredService<SelectionState>();

        Assert.Null(selection.Selected);
        var row = list.FindAll(".step-row").First();
        var expectedId = row.GetAttribute("data-step-id");
        row.Click();

        Assert.NotNull(selection.Selected);
        Assert.Equal(expectedId, selection.Selected!.Id);
    }

    [Fact]
    public void StepList_StarClick_TogglesFavouriteWithoutSelecting()
    {
        var (ctx, _, _, favs) = NewContext();
        using var _d = ctx;
        ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        var list = ctx.RenderComponent<StepList>();
        var selection = ctx.Services.GetRequiredService<SelectionState>();

        Assert.Null(selection.Selected);
        var row = list.FindAll(".step-row").First();
        var stepId = row.GetAttribute("data-step-id")!;
        row.QuerySelector(".row-star")!.Click();

        Assert.Null(selection.Selected);
        Assert.True(favs.Has(stepId));
    }

    [Fact]
    public void Combined_TypeDomainAndQuery_NarrowsDeterministically()
    {
        var (ctx, store, state, favs) = NewContext();
        using var _ = ctx;
        var header = ctx.RenderComponent<Header>();
        var rail = ctx.RenderComponent<LeftRail>();

        // Deselect all types except Given.
        rail.Find("button.step-type[data-type='When']").Click();
        rail.Find("button.step-type[data-type='Then']").Click();

        rail.Find("button.domain-row[data-domain='Auth']").Click();
        header.Find("input.search-input").Input("logged");

        header.WaitForAssertion(
            () => Assert.Single(FilterEngine.Apply(store.Steps, state, favs)),
            timeout: TimeSpan.FromMilliseconds(500));

        var projected = FilterEngine.Apply(store.Steps, state, favs);
        Assert.Equal("g1", projected[0].Id);
    }

    [Fact]
    public void DetailPanel_EmptyBy_Default_When_No_Selection()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        ctx.RenderComponent<StepList>();
        var detail = ctx.RenderComponent<DetailPanel>();

        Assert.NotNull(detail.Find("[data-testid='detail-empty']"));
        Assert.Empty(detail.FindAll("[data-testid='detail-panel']"));
    }

    [Fact]
    public void Row_Click_Populates_DetailPanel_With_Same_Step()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        var list = ctx.RenderComponent<StepList>();
        var detail = ctx.RenderComponent<DetailPanel>();

        var row = list.FindAll(".step-row").First();
        var expectedId = row.GetAttribute("data-step-id");
        row.Click();

        detail.WaitForAssertion(
            () =>
            {
                var panel = detail.Find("[data-testid='detail-panel']");
                Assert.Equal(expectedId, panel.GetAttribute("data-step-id"));
            },
            timeout: TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void Row_Star_Click_Reflects_In_Detail_Favourite_Button()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        var list = ctx.RenderComponent<StepList>();
        var detail = ctx.RenderComponent<DetailPanel>();

        var row = list.FindAll(".step-row").First();
        row.Click();

        // Star click on the row.
        list.FindAll(".step-row").First().QuerySelector(".row-star")!.Click();

        detail.WaitForAssertion(
            () =>
            {
                var fav = detail.Find("[data-testid='detail-favourite']");
                Assert.Contains("is-fav", fav.GetAttribute("class") ?? "");
            },
            timeout: TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void J_With_No_Selection_Selects_First_Filtered()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        ctx.RenderComponent<StepList>();
        var keyboard = ctx.RenderComponent<KeyboardHandler>();
        var provider = ctx.Services.GetRequiredService<FilteredStepsProvider>();
        var selection = ctx.Services.GetRequiredService<SelectionState>();

        keyboard.Instance.OnKey("select-next");

        Assert.NotNull(selection.Selected);
        Assert.Equal(provider.Filtered[0].Id, selection.Selected!.Id);
    }

    [Fact]
    public void J_Then_J_Moves_To_Second_Row()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        ctx.RenderComponent<StepList>();
        var keyboard = ctx.RenderComponent<KeyboardHandler>();
        var provider = ctx.Services.GetRequiredService<FilteredStepsProvider>();
        var selection = ctx.Services.GetRequiredService<SelectionState>();

        keyboard.Instance.OnKey("select-next");
        keyboard.Instance.OnKey("select-next");

        Assert.Equal(provider.Filtered[1].Id, selection.Selected!.Id);
    }

    [Fact]
    public void K_From_First_Item_Is_NoOp()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        ctx.RenderComponent<StepList>();
        var keyboard = ctx.RenderComponent<KeyboardHandler>();
        var provider = ctx.Services.GetRequiredService<FilteredStepsProvider>();
        var selection = ctx.Services.GetRequiredService<SelectionState>();

        selection.Select(provider.Filtered[0]);
        keyboard.Instance.OnKey("select-prev");

        Assert.Equal(provider.Filtered[0].Id, selection.Selected!.Id);
    }

    [Fact]
    public void J_At_Last_Item_Is_NoOp_NoWrap()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        ctx.RenderComponent<StepList>();
        var keyboard = ctx.RenderComponent<KeyboardHandler>();
        var provider = ctx.Services.GetRequiredService<FilteredStepsProvider>();
        var selection = ctx.Services.GetRequiredService<SelectionState>();

        var last = provider.Filtered[^1];
        selection.Select(last);
        keyboard.Instance.OnKey("select-next");

        Assert.Equal(last.Id, selection.Selected!.Id);
    }

    [Fact]
    public void F_With_Selection_Toggles_Favourite()
    {
        var (ctx, _, _, favs) = NewContext();
        using var _d = ctx;
        ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        ctx.RenderComponent<StepList>();
        var detail = ctx.RenderComponent<DetailPanel>();
        var keyboard = ctx.RenderComponent<KeyboardHandler>();
        var provider = ctx.Services.GetRequiredService<FilteredStepsProvider>();
        var selection = ctx.Services.GetRequiredService<SelectionState>();

        var target = provider.Filtered[0];
        selection.Select(target);

        keyboard.Instance.OnKey("toggle-fav");

        Assert.True(favs.Has(target.Id));
        detail.WaitForAssertion(
            () => Assert.Contains("is-fav", detail.Find("[data-testid='detail-favourite']").GetAttribute("class") ?? ""),
            timeout: TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void F_With_No_Selection_Does_Not_Toggle_Anything()
    {
        var (ctx, _, _, favs) = NewContext();
        using var _d = ctx;
        ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        ctx.RenderComponent<StepList>();
        var keyboard = ctx.RenderComponent<KeyboardHandler>();

        keyboard.Instance.OnKey("toggle-fav");

        Assert.Equal(0, favs.Count);
    }

    [Fact]
    public void OpenPalette_Raises_OpenPaletteRequested()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        ctx.RenderComponent<StepList>();
        var keyboard = ctx.RenderComponent<KeyboardHandler>();
        var actions = ctx.Services.GetRequiredService<IKeyboardActions>();

        var count = 0;
        actions.OpenPaletteRequested += () => count++;

        keyboard.Instance.OnKey("open-palette");

        Assert.Equal(1, count);
    }

    [Fact]
    public void OpenShortcuts_Raises_OpenShortcutsRequested()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        ctx.RenderComponent<StepList>();
        var keyboard = ctx.RenderComponent<KeyboardHandler>();
        var actions = ctx.Services.GetRequiredService<IKeyboardActions>();

        var count = 0;
        actions.OpenShortcutsRequested += () => count++;

        keyboard.Instance.OnKey("open-shortcuts");

        Assert.Equal(1, count);
    }

    [Fact]
    public void ToggleComposer_Raises_ToggleComposerRequested()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        ctx.RenderComponent<StepList>();
        var keyboard = ctx.RenderComponent<KeyboardHandler>();
        var actions = ctx.Services.GetRequiredService<IKeyboardActions>();

        var count = 0;
        actions.ToggleComposerRequested += () => count++;

        keyboard.Instance.OnKey("toggle-composer");

        Assert.Equal(1, count);
    }

    [Fact]
    public void CloseOverlay_Raises_CloseOverlayRequested()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        ctx.RenderComponent<StepList>();
        var keyboard = ctx.RenderComponent<KeyboardHandler>();
        var actions = ctx.Services.GetRequiredService<IKeyboardActions>();

        var count = 0;
        actions.CloseOverlayRequested += () => count++;

        keyboard.Instance.OnKey("close-overlay");

        Assert.Equal(1, count);
    }

    [Fact]
    public void SelectNext_After_Filter_Excludes_Current_Selection_Picks_First_Filtered()
    {
        var (ctx, _, state, _) = NewContext();
        using var _d = ctx;
        ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        ctx.RenderComponent<StepList>();
        var keyboard = ctx.RenderComponent<KeyboardHandler>();
        var provider = ctx.Services.GetRequiredService<FilteredStepsProvider>();
        var selection = ctx.Services.GetRequiredService<SelectionState>();

        // Select an Auth step.
        var authStep = provider.Filtered.First(s => s.Domain == "Auth");
        selection.Select(authStep);

        // Filter to Billing — selection is no longer in the filtered list.
        state.SetDomain("Billing");

        keyboard.Instance.OnKey("select-next");

        Assert.Equal(provider.Filtered[0].Id, selection.Selected!.Id);
        Assert.Equal("Billing", selection.Selected!.Domain);
    }

    [Fact]
    public void Related_Card_Click_Changes_Selection()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        var list = ctx.RenderComponent<StepList>();
        var detail = ctx.RenderComponent<DetailPanel>();
        var selection = ctx.Services.GetRequiredService<SelectionState>();

        // Select g1 (which has SuggestsNext = ["g2"]).
        list.Find(".step-row[data-step-id='g1']").Click();

        detail.WaitForAssertion(
            () => Assert.NotEmpty(detail.FindAll("[data-testid='related-card']")),
            timeout: TimeSpan.FromMilliseconds(500));

        detail.Find("[data-testid='related-card']").Click();

        detail.WaitForAssertion(
            () =>
            {
                Assert.NotNull(selection.Selected);
                Assert.Equal("g2", selection.Selected!.Id);
                Assert.Equal("g2", detail.Find("[data-testid='detail-panel']").GetAttribute("data-step-id"));
            },
            timeout: TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void Palette_Opens_When_Keyboard_Handler_Receives_OpenPalette()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        ctx.RenderComponent<StepList>();
        ctx.RenderComponent<DetailPanel>();
        var palette = ctx.RenderComponent<Palette>();
        var keyboard = ctx.RenderComponent<KeyboardHandler>();

        keyboard.Instance.OnKey(KeyboardActionNames.OpenPalette);

        palette.WaitForAssertion(
            () => Assert.Single(palette.FindAll("[data-testid='palette']")),
            timeout: TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void Palette_Opens_When_QuickFind_Button_Clicked()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        var header = ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        ctx.RenderComponent<StepList>();
        ctx.RenderComponent<DetailPanel>();
        var palette = ctx.RenderComponent<Palette>();
        ctx.RenderComponent<KeyboardHandler>();

        header.Find("[data-testid='quick-find']").Click();

        palette.WaitForAssertion(
            () => Assert.Single(palette.FindAll("[data-testid='palette']")),
            timeout: TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void Palette_Typing_Filters_Results()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        var header = ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        ctx.RenderComponent<StepList>();
        ctx.RenderComponent<DetailPanel>();
        var palette = ctx.RenderComponent<Palette>();
        ctx.RenderComponent<KeyboardHandler>();

        header.Find("[data-testid='quick-find']").Click();

        palette.WaitForAssertion(
            () => Assert.Single(palette.FindAll("[data-testid='palette']")),
            timeout: TimeSpan.FromMilliseconds(500));

        palette.Find("[data-testid='palette-input']").Input("dashboard");

        palette.WaitForAssertion(
            () =>
            {
                var results = palette.FindAll("[data-testid='palette-result']");
                Assert.Single(results);
                Assert.Equal("g5", results[0].GetAttribute("data-step-id"));
            },
            timeout: TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void Palette_Enter_Selects_And_Closes()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        var header = ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        ctx.RenderComponent<StepList>();
        var detail = ctx.RenderComponent<DetailPanel>();
        var palette = ctx.RenderComponent<Palette>();
        ctx.RenderComponent<KeyboardHandler>();
        var selection = ctx.Services.GetRequiredService<SelectionState>();

        header.Find("[data-testid='quick-find']").Click();

        palette.WaitForAssertion(
            () => Assert.Single(palette.FindAll("[data-testid='palette']")),
            timeout: TimeSpan.FromMilliseconds(500));

        palette.Find("[data-testid='palette-input']")
               .KeyDown(new KeyboardEventArgs { Key = "Enter" });

        palette.WaitForAssertion(
            () => Assert.Empty(palette.FindAll("[data-testid='palette']")),
            timeout: TimeSpan.FromMilliseconds(500));

        Assert.NotNull(selection.Selected);
        Assert.Equal("g3", selection.Selected!.Id);

        detail.WaitForAssertion(
            () => Assert.Equal("g3", detail.Find("[data-testid='detail-panel']").GetAttribute("data-step-id")),
            timeout: TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void Palette_Escape_Closes_Without_Selection()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        var header = ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        ctx.RenderComponent<StepList>();
        ctx.RenderComponent<DetailPanel>();
        var palette = ctx.RenderComponent<Palette>();
        ctx.RenderComponent<KeyboardHandler>();
        var selection = ctx.Services.GetRequiredService<SelectionState>();

        header.Find("[data-testid='quick-find']").Click();

        palette.WaitForAssertion(
            () => Assert.Single(palette.FindAll("[data-testid='palette']")),
            timeout: TimeSpan.FromMilliseconds(500));

        Assert.Null(selection.Selected);

        palette.Find("[data-testid='palette-input']")
               .KeyDown(new KeyboardEventArgs { Key = "Escape" });

        palette.WaitForAssertion(
            () => Assert.Empty(palette.FindAll("[data-testid='palette']")),
            timeout: TimeSpan.FromMilliseconds(500));

        Assert.Null(selection.Selected);
    }

    [Fact]
    public void Palette_Backdrop_Click_Closes()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        var header = ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        ctx.RenderComponent<StepList>();
        ctx.RenderComponent<DetailPanel>();
        var palette = ctx.RenderComponent<Palette>();
        ctx.RenderComponent<KeyboardHandler>();

        header.Find("[data-testid='quick-find']").Click();

        palette.WaitForAssertion(
            () => Assert.Single(palette.FindAll("[data-testid='palette']")),
            timeout: TimeSpan.FromMilliseconds(500));

        palette.Find("[data-testid='palette-backdrop']").Click();

        palette.WaitForAssertion(
            () => Assert.Empty(palette.FindAll("[data-testid='palette']")),
            timeout: TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void Palette_Empty_Result_Shows_Hint()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        var header = ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        ctx.RenderComponent<StepList>();
        ctx.RenderComponent<DetailPanel>();
        var palette = ctx.RenderComponent<Palette>();
        ctx.RenderComponent<KeyboardHandler>();

        header.Find("[data-testid='quick-find']").Click();

        palette.WaitForAssertion(
            () => Assert.Single(palette.FindAll("[data-testid='palette']")),
            timeout: TimeSpan.FromMilliseconds(500));

        palette.Find("[data-testid='palette-input']").Input("qqqzzzxxx");

        palette.WaitForAssertion(
            () =>
            {
                Assert.Single(palette.FindAll("[data-testid='palette-empty']"));
                Assert.Empty(palette.FindAll("[data-testid='palette-result']"));
            },
            timeout: TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void Palette_Default_Shows_Top_Usage_When_Empty_Query()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        var header = ctx.RenderComponent<Header>();
        ctx.RenderComponent<LeftRail>();
        ctx.RenderComponent<StepList>();
        ctx.RenderComponent<DetailPanel>();
        var palette = ctx.RenderComponent<Palette>();
        ctx.RenderComponent<KeyboardHandler>();

        header.Find("[data-testid='quick-find']").Click();

        palette.WaitForAssertion(
            () =>
            {
                var results = palette.FindAll("[data-testid='palette-result']");
                Assert.NotEmpty(results);
                Assert.Equal("g3", results[0].GetAttribute("data-step-id"));
            },
            timeout: TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void Shortcuts_Opens_When_KeyboardHandler_Receives_OpenShortcuts()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        var overlay = ctx.RenderComponent<ShortcutsOverlay>();
        var keyboard = ctx.RenderComponent<KeyboardHandler>();
        ctx.RenderComponent<Header>();

        keyboard.Instance.OnKey("open-shortcuts");

        overlay.WaitForAssertion(
            () => Assert.Single(overlay.FindAll("[data-testid='shortcuts-dialog']")),
            timeout: TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void Shortcuts_Opens_When_Header_Button_Clicked()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        var overlay = ctx.RenderComponent<ShortcutsOverlay>();
        ctx.RenderComponent<KeyboardHandler>();
        var header = ctx.RenderComponent<Header>();

        header.Find("[data-testid='shortcuts-button']").Click();

        overlay.WaitForAssertion(
            () => Assert.Single(overlay.FindAll("[data-testid='shortcuts-dialog']")),
            timeout: TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void Shortcuts_Escape_On_Dialog_Closes()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        var overlay = ctx.RenderComponent<ShortcutsOverlay>();
        var keyboard = ctx.RenderComponent<KeyboardHandler>();
        ctx.RenderComponent<Header>();

        keyboard.Instance.OnKey("open-shortcuts");

        overlay.WaitForAssertion(
            () => Assert.Single(overlay.FindAll("[data-testid='shortcuts-dialog']")),
            timeout: TimeSpan.FromMilliseconds(500));

        overlay.Find("[data-testid='shortcuts-dialog']")
               .KeyDown(new KeyboardEventArgs { Key = "Escape" });

        overlay.WaitForAssertion(
            () => Assert.Empty(overlay.FindAll("[data-testid='shortcuts-dialog']")),
            timeout: TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void Shortcuts_Backdrop_Click_Closes()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        var overlay = ctx.RenderComponent<ShortcutsOverlay>();
        var keyboard = ctx.RenderComponent<KeyboardHandler>();
        ctx.RenderComponent<Header>();

        keyboard.Instance.OnKey("open-shortcuts");

        overlay.WaitForAssertion(
            () => Assert.Single(overlay.FindAll("[data-testid='shortcuts-dialog']")),
            timeout: TimeSpan.FromMilliseconds(500));

        overlay.Find("[data-testid='shortcuts-backdrop']").Click();

        overlay.WaitForAssertion(
            () => Assert.Empty(overlay.FindAll("[data-testid='shortcuts-dialog']")),
            timeout: TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void Shortcuts_Lists_Seven_Shortcuts()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        var overlay = ctx.RenderComponent<ShortcutsOverlay>();
        var keyboard = ctx.RenderComponent<KeyboardHandler>();
        ctx.RenderComponent<Header>();

        keyboard.Instance.OnKey("open-shortcuts");

        overlay.WaitForAssertion(
            () => Assert.Equal(7, overlay.FindAll("[data-testid='shortcut-row']").Count),
            timeout: TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void Tweaks_Opens_When_Gear_Clicked()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        var header = ctx.RenderComponent<Header>();
        var panel = ctx.RenderComponent<TweaksPanel>();

        header.Find("[data-testid='tweaks-button']").Click();

        panel.WaitForAssertion(
            () => Assert.Single(panel.FindAll("[data-testid='tweaks-panel']")),
            timeout: TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void Tweaks_Accent_Change_Updates_Store()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        var header = ctx.RenderComponent<Header>();
        var panel = ctx.RenderComponent<TweaksPanel>();
        var tweaks = ctx.Services.GetRequiredService<TweaksStore>();

        header.Find("[data-testid='tweaks-button']").Click();

        panel.WaitForAssertion(
            () => Assert.Single(panel.FindAll("[data-testid='tweaks-panel']")),
            timeout: TimeSpan.FromMilliseconds(500));

        panel.Find("[data-testid='tweaks-accent-blue']").Change(true);

        Assert.Equal(AccentOption.Blue, tweaks.Accent);
    }

    [Fact]
    public void Tweaks_Escape_Closes()
    {
        var (ctx, _, _, _) = NewContext();
        using var _d = ctx;
        var header = ctx.RenderComponent<Header>();
        var panel = ctx.RenderComponent<TweaksPanel>();

        header.Find("[data-testid='tweaks-button']").Click();

        panel.WaitForAssertion(
            () => Assert.Single(panel.FindAll("[data-testid='tweaks-panel']")),
            timeout: TimeSpan.FromMilliseconds(500));

        panel.Find("[data-testid='tweaks-panel']")
             .KeyDown(new KeyboardEventArgs { Key = "Escape" });

        panel.WaitForAssertion(
            () => Assert.Empty(panel.FindAll("[data-testid='tweaks-panel']")),
            timeout: TimeSpan.FromMilliseconds(500));
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
}
