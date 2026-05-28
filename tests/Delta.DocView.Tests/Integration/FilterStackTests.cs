using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Client.Layout;
using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

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
                SuggestsNext = ["g2"]
            },
            new Step
            {
                Id = "g2",
                Type = "Given",
                Domain = "Auth",
                Pattern = "I have {int} active sessions",
                Params = [new StepParam { Name = "count", Type = "int" }]
            },
            new Step
            {
                Id = "g3",
                Type = "When",
                Domain = "Billing",
                Pattern = "I add card ending {string}",
                Params = [new StepParam { Name = "last4", Type = "string" }]
            },
            new Step
            {
                Id = "g4",
                Type = "When",
                Domain = "Billing",
                Pattern = "I post a payment of {decimal}",
                Params = [new StepParam { Name = "amount", Type = "decimal" }]
            },
            new Step
            {
                Id = "g5",
                Type = "Then",
                Domain = "Auth",
                Pattern = "I see the dashboard",
                Params = []
            },
            new Step
            {
                Id = "g6",
                Type = "And",
                Domain = "Billing",
                Pattern = "I receive a receipt for {string}",
                Params = [new StepParam { Name = "ref", Type = "string" }]
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
        rail.Find("button.step-type[data-type='And']").Click();

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
}
