using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Delta.DocView.Tests.Components;

public class StepRowTests
{
    private static (TestContext ctx, IFavouritesStore favs, SelectionState sel) Setup()
    {
        var ctx = new TestContext();
        ctx.Services.AddScoped<IFavouritesStore, InMemoryFavouritesStore>();
        ctx.Services.AddScoped<SelectionState>();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var favs = ctx.Services.GetRequiredService<IFavouritesStore>();
        var sel = ctx.Services.GetRequiredService<SelectionState>();
        return (ctx, favs, sel);
    }

    private static Step MakeStep(
        string id = "step-1",
        string type = "Given",
        string pattern = "the user {name:string} signs in",
        string file = "Login.feature",
        int line = 12,
        IReadOnlyList<string>? tags = null,
        int used = 3)
    {
        return new Step
        {
            Id = id,
            Type = type,
            Pattern = pattern,
            File = file,
            Line = line,
            Tags = tags ?? Array.Empty<string>(),
            Used = used,
        };
    }

    [Fact]
    public void Renders_TypeChip_With_Lowercased_Class_And_Original_Label()
    {
        var (ctx, _, _) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<StepRow>(p => p.Add(x => x.Step, MakeStep(type: "Given")));

        var chip = cut.Find(".type-chip");
        Assert.Contains("chip-given", chip.GetAttribute("class"));
        Assert.Equal("Given", chip.TextContent);
    }

    [Fact]
    public void Pattern_Is_Rendered_Via_PatternRenderer()
    {
        var (ctx, _, _) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<StepRow>(p => p.Add(x => x.Step, MakeStep(pattern: "the user {name:string} signs in")));

        var patternEl = cut.Find(".step-pattern");
        Assert.NotNull(patternEl);
        Assert.NotEmpty(patternEl.QuerySelectorAll(".param-pill"));
    }

    [Fact]
    public void Tags_Up_To_Three_Render_Joined()
    {
        var (ctx, _, _) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<StepRow>(p => p.Add(x => x.Step, MakeStep(tags: new[] { "a", "b", "c" })));

        Assert.Equal("a · b · c", cut.Find(".step-tags").TextContent);
    }

    [Fact]
    public void More_Than_Three_Tags_Show_Plus_N_Overflow()
    {
        var (ctx, _, _) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<StepRow>(p => p.Add(x => x.Step, MakeStep(tags: new[] { "a", "b", "c", "d", "e" })));

        Assert.Equal("a · b · c +2", cut.Find(".step-tags").TextContent);
    }

    [Fact]
    public void Zero_Tags_Renders_No_Tags_Element()
    {
        var (ctx, _, _) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<StepRow>(p => p.Add(x => x.Step, MakeStep(tags: Array.Empty<string>())));

        Assert.Empty(cut.FindAll(".step-tags"));
    }

    [Fact]
    public void IsSelected_True_Adds_Is_Selected_Class()
    {
        var (ctx, _, _) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<StepRow>(p => p
            .Add(x => x.Step, MakeStep())
            .Add(x => x.IsSelected, true));

        Assert.Contains("is-selected", cut.Find(".step-row").GetAttribute("class"));
    }

    [Fact]
    public void Click_On_Row_Selects_Step()
    {
        var (ctx, _, sel) = Setup();
        using var _c = ctx;
        var step = MakeStep(id: "step-xyz");
        var cut = ctx.RenderComponent<StepRow>(p => p.Add(x => x.Step, step));

        cut.Find(".step-row").Click();

        Assert.NotNull(sel.Selected);
        Assert.Equal("step-xyz", sel.Selected!.Id);
    }

    [Fact]
    public void Click_On_Star_Toggles_Fav_And_Does_Not_Select()
    {
        var (ctx, favs, sel) = Setup();
        using var _c = ctx;
        var step = MakeStep(id: "step-star");
        var cut = ctx.RenderComponent<StepRow>(p => p.Add(x => x.Step, step));

        cut.Find("[data-testid=\"row-star\"]").Click();

        Assert.True(favs.Has("step-star"));
        Assert.Null(sel.Selected);
    }

    [Fact]
    public void External_Toggle_Updates_Star_Class_Via_Subscription()
    {
        var (ctx, favs, _) = Setup();
        using var _c = ctx;
        var step = MakeStep(id: "step-ext");
        var cut = ctx.RenderComponent<StepRow>(p => p.Add(x => x.Step, step));

        Assert.DoesNotContain("is-fav", cut.Find("[data-testid=\"row-star\"]").GetAttribute("class"));

        favs.Toggle("step-ext");

        Assert.Contains("is-fav", cut.Find("[data-testid=\"row-star\"]").GetAttribute("class"));
    }

    [Fact]
    public void Add_Button_Is_Disabled_Placeholder()
    {
        var (ctx, _, _) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<StepRow>(p => p.Add(x => x.Step, MakeStep()));

        var addBtn = cut.Find("[data-testid=\"row-add\"]");
        Assert.True(addBtn.HasAttribute("disabled"));
    }
}
