using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Client.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Delta.DocView.Tests.Components;

public class FavouritesToggleTests
{
    private static (TestContext ctx, FilterState state, IFavouritesStore favs) Setup()
    {
        var ctx = new TestContext();
        ctx.Services.AddScoped<ClientStepLibraryStore>();
        ctx.Services.AddScoped<FilterState>();
        ctx.Services.AddScoped<IFavouritesStore, InMemoryFavouritesStore>();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var state = ctx.Services.GetRequiredService<FilterState>();
        var favs = ctx.Services.GetRequiredService<IFavouritesStore>();
        return (ctx, state, favs);
    }

    [Fact]
    public void Renders_With_Zero_Count_And_Not_Active_By_Default()
    {
        var (ctx, _, _) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<FavouritesToggle>();

        var btn = cut.Find("button[data-testid=\"favourites-toggle\"]");
        Assert.DoesNotContain("is-active", btn.GetAttribute("class"));
        Assert.Equal("0", cut.Find("button[data-testid=\"favourites-toggle\"] .count").TextContent);
    }

    [Fact]
    public void Clicking_Sets_FavsOnly_True_And_Adds_Active_Class()
    {
        var (ctx, state, _) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<FavouritesToggle>();

        cut.Find("button[data-testid=\"favourites-toggle\"]").Click();

        Assert.True(state.FavsOnly);
        Assert.Contains("is-active", cut.Find("button[data-testid=\"favourites-toggle\"]").GetAttribute("class"));
    }

    [Fact]
    public void Clicking_Again_Clears_FavsOnly()
    {
        var (ctx, state, _) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<FavouritesToggle>();

        cut.Find("button[data-testid=\"favourites-toggle\"]").Click();
        cut.Find("button[data-testid=\"favourites-toggle\"]").Click();

        Assert.False(state.FavsOnly);
        Assert.DoesNotContain("is-active", cut.Find("button[data-testid=\"favourites-toggle\"]").GetAttribute("class"));
    }

    [Fact]
    public void Count_Reflects_Favourites_Added_Via_Store()
    {
        var (ctx, _, favs) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<FavouritesToggle>();

        favs.Toggle("step-1");
        favs.Toggle("step-2");

        Assert.Equal("2", cut.Find("button[data-testid=\"favourites-toggle\"] .count").TextContent);
    }

    [Fact]
    public void Count_Decreases_When_Favourite_Removed()
    {
        var (ctx, _, favs) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<FavouritesToggle>();

        favs.Toggle("step-1");
        favs.Toggle("step-2");
        favs.Toggle("step-1");

        Assert.Equal("1", cut.Find("button[data-testid=\"favourites-toggle\"] .count").TextContent);
    }
}
