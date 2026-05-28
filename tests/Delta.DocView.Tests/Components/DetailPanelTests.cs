using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Delta.DocView.Tests.Components;

public class DetailPanelTests
{
    private static Step MakeStepA() => new()
    {
        Id = "stepA",
        Type = "Given",
        Pattern = "the user {name} is signed in",
        Params = new List<StepParam>
        {
            new() { Name = "name", Type = "string", Example = "Alice" },
        },
        File = "Auth.cs",
        Line = 10,
        Domain = "auth",
        Tags = new[] { "login" },
        Used = 3,
        Description = "Signs the named user in.",
        Source = "public void GivenUserIsSignedIn(string name) {}",
        SuggestsNext = Array.Empty<string>(),
    };

    private static Step MakeStepB() => new()
    {
        Id = "stepB",
        Type = "When",
        Pattern = "they click logout",
        Params = Array.Empty<StepParam>(),
        File = "Auth.cs",
        Line = 50,
        Domain = "auth",
        Tags = Array.Empty<string>(),
        Used = 1,
        Description = "",
        Source = "public void WhenClickLogout() {}",
        SuggestsNext = Array.Empty<string>(),
    };

    private static (TestContext ctx, ClientStepLibraryStore store, SelectionState sel, IFavouritesStore favs)
        Setup(IEnumerable<Step>? steps = null)
    {
        var ctx = new TestContext();
        ctx.Services.AddScoped<ClientStepLibraryStore>();
        ctx.Services.AddScoped<SelectionState>();
        ctx.Services.AddScoped<IFavouritesStore, InMemoryFavouritesStore>();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var store = ctx.Services.GetRequiredService<ClientStepLibraryStore>();
        store.Populate(new StepLibrary
        {
            Steps = (steps ?? new[] { MakeStepA(), MakeStepB() }).ToList(),
            Domains = new List<StepDomain> { new() { Id = "auth", Label = "Auth" } },
        });
        var sel = ctx.Services.GetRequiredService<SelectionState>();
        var favs = ctx.Services.GetRequiredService<IFavouritesStore>();
        return (ctx, store, sel, favs);
    }

    [Fact]
    public void No_Selection_Renders_Empty_State()
    {
        var (ctx, _, _, _) = Setup();

        var cut = ctx.RenderComponent<DetailPanel>();

        Assert.NotNull(cut.Find("[data-testid='detail-empty']"));
        Assert.Empty(cut.FindAll("[data-testid='detail-panel']"));
    }

    [Fact]
    public void Selected_Step_Renders_Detail_Panel()
    {
        var step = MakeStepA();
        var (ctx, _, sel, _) = Setup(new[] { step });
        sel.Select(step);

        var cut = ctx.RenderComponent<DetailPanel>();

        var panel = cut.Find("[data-testid='detail-panel']");
        Assert.Equal(step.Id, panel.GetAttribute("data-step-id"));
    }

    [Fact]
    public void Header_Type_Chip_Has_Correct_Class()
    {
        var step = MakeStepA();
        var (ctx, _, sel, _) = Setup(new[] { step });
        sel.Select(step);

        var cut = ctx.RenderComponent<DetailPanel>();

        var chip = cut.Find(".detail-header .type-chip.chip-given");
        Assert.Equal("Given", chip.TextContent);
    }

    [Fact]
    public void Favourite_Button_Toggles_Favs()
    {
        var step = MakeStepA();
        var (ctx, _, sel, favs) = Setup(new[] { step });
        sel.Select(step);

        var cut = ctx.RenderComponent<DetailPanel>();

        cut.Find("[data-testid='detail-favourite']").Click();
        Assert.True(favs.Has(step.Id));

        cut.Find("[data-testid='detail-favourite']").Click();
        Assert.False(favs.Has(step.Id));
    }

    [Fact]
    public void Favourite_Button_Reflects_External_Toggle()
    {
        var step = MakeStepA();
        var (ctx, _, sel, favs) = Setup(new[] { step });
        sel.Select(step);

        var cut = ctx.RenderComponent<DetailPanel>();
        Assert.DoesNotContain("is-fav", cut.Find("[data-testid='detail-favourite']").GetAttribute("class") ?? "");

        favs.Toggle(step.Id);

        var classAttr = cut.Find("[data-testid='detail-favourite']").GetAttribute("class") ?? "";
        Assert.Contains("is-fav", classAttr);
    }

    [Fact]
    public void Add_To_Scenario_Button_Is_Disabled()
    {
        var step = MakeStepA();
        var (ctx, _, sel, _) = Setup(new[] { step });
        sel.Select(step);

        var cut = ctx.RenderComponent<DetailPanel>();

        var btn = cut.Find("[data-testid='detail-add-primary']");
        Assert.True(btn.HasAttribute("disabled"));
        var title = btn.GetAttribute("title") ?? "";
        Assert.Contains("coming soon", title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Selection_Change_Updates_Panel()
    {
        var stepA = MakeStepA();
        var stepB = MakeStepB();
        var (ctx, _, sel, _) = Setup(new[] { stepA, stepB });
        sel.Select(stepA);

        var cut = ctx.RenderComponent<DetailPanel>();
        Assert.Equal(stepA.Id, cut.Find("[data-testid='detail-panel']").GetAttribute("data-step-id"));

        sel.Select(stepB);

        Assert.Equal(stepB.Id, cut.Find("[data-testid='detail-panel']").GetAttribute("data-step-id"));
    }

    [Fact]
    public void Favourite_Button_Has_Aria_Label_And_Pressed_State()
    {
        var step = MakeStepA();
        var (ctx, _, sel, _) = Setup(new[] { step });
        sel.Select(step);

        var cut = ctx.RenderComponent<DetailPanel>();

        var btn = cut.Find("[data-testid='detail-favourite']");
        Assert.Equal("Toggle favourite", btn.GetAttribute("aria-label"));
        Assert.Equal("false", btn.GetAttribute("aria-pressed"));

        btn.Click();

        Assert.Equal("true", cut.Find("[data-testid='detail-favourite']").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Description_Hidden_When_Empty()
    {
        var step = MakeStepB(); // Description = ""
        var (ctx, _, sel, _) = Setup(new[] { step });
        sel.Select(step);

        var cut = ctx.RenderComponent<DetailPanel>();

        Assert.Empty(cut.FindAll(".detail-description"));
    }
}
