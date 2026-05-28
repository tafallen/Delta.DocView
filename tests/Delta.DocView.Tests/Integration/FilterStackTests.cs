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
                Params = [new StepParam { Name = "user", Type = "string" }]
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
}
