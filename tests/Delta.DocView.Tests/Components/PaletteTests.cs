using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Delta.DocView.Tests.Components;

public class PaletteTests
{
    private static Step MakeStep(string id, string type, string pattern, string domain, int used) => new()
    {
        Id = id,
        Type = type,
        Pattern = pattern,
        Params = Array.Empty<StepParam>(),
        File = "Auth.cs",
        Line = 1,
        Domain = domain,
        Tags = Array.Empty<string>(),
        Used = used,
        Description = "",
        Source = "",
        SuggestsNext = Array.Empty<string>(),
    };

    private static IReadOnlyList<Step> DefaultSteps() => new[]
    {
        MakeStep("s1", "Given", "the user is logged in", "auth", 50),
        MakeStep("s2", "When",  "they click login",      "auth", 20),
        MakeStep("s3", "Then",  "they see the dashboard","ui",   10),
    };

    private static (TestContext ctx, PaletteState palette, IKeyboardActions actions, SelectionState selection)
        Setup(IEnumerable<Step>? steps = null)
    {
        var ctx = new TestContext();
        ctx.Services.AddScoped<ClientStepLibraryStore>();
        ctx.Services.AddScoped<IFavouritesStore, InMemoryFavouritesStore>();
        ctx.Services.AddScoped<FilterState>();
        ctx.Services.AddScoped<SelectionState>();
        ctx.Services.AddScoped<FilteredStepsProvider>();
        ctx.Services.AddScoped<IKeyboardActions, KeyboardActions>();
        ctx.Services.AddScoped<PaletteState>();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var store = ctx.Services.GetRequiredService<ClientStepLibraryStore>();
        store.Populate(new StepLibrary
        {
            Steps = (steps ?? DefaultSteps()).ToList(),
            Domains = new List<StepDomain>
            {
                new() { Id = "auth", Label = "Auth" },
                new() { Id = "ui",   Label = "UI" },
            },
        });

        var palette = ctx.Services.GetRequiredService<PaletteState>();
        var actions = ctx.Services.GetRequiredService<IKeyboardActions>();
        var selection = ctx.Services.GetRequiredService<SelectionState>();
        return (ctx, palette, actions, selection);
    }

    [Fact]
    public void Renders_Nothing_When_Closed()
    {
        var (ctx, _, _, _) = Setup();

        var cut = ctx.RenderComponent<Palette>();

        Assert.Empty(cut.FindAll("[data-testid='palette']"));
    }

    [Fact]
    public void Renders_Backdrop_And_Modal_When_Open()
    {
        var (ctx, _, actions, _) = Setup();
        var cut = ctx.RenderComponent<Palette>();

        actions.OpenPalette();

        Assert.NotNull(cut.Find("[data-testid='palette-backdrop']"));
        Assert.NotNull(cut.Find("[data-testid='palette']"));
    }

    [Fact]
    public void Default_Open_Shows_TopByUsage()
    {
        var (ctx, _, actions, _) = Setup();
        var cut = ctx.RenderComponent<Palette>();

        actions.OpenPalette();

        var first = cut.FindAll("[data-testid='palette-result']")[0];
        Assert.Equal("s1", first.GetAttribute("data-step-id"));
    }

    [Fact]
    public void Typing_In_Input_Updates_Query_And_Filters_Results()
    {
        var (ctx, _, actions, _) = Setup();
        var cut = ctx.RenderComponent<Palette>();
        actions.OpenPalette();

        cut.Find("[data-testid='palette-input']").Input("login");

        var results = cut.FindAll("[data-testid='palette-result']");
        // Only the two auth-related "login" steps should match.
        Assert.All(results, r =>
        {
            var id = r.GetAttribute("data-step-id");
            Assert.Contains(id, new[] { "s1", "s2" });
        });
        Assert.True(results.Count < 3);
    }

    [Fact]
    public void ArrowDown_Moves_Selection()
    {
        var (ctx, _, actions, _) = Setup();
        var cut = ctx.RenderComponent<Palette>();
        actions.OpenPalette();

        cut.Find("[data-testid='palette-input']").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        var results = cut.FindAll("[data-testid='palette-result']");
        Assert.Contains("is-active", results[1].GetAttribute("class") ?? "");
        Assert.DoesNotContain("is-active", results[0].GetAttribute("class") ?? "");
    }

    [Fact]
    public void ArrowUp_Moves_Selection_Up()
    {
        var (ctx, _, actions, _) = Setup();
        var cut = ctx.RenderComponent<Palette>();
        actions.OpenPalette();

        var input = cut.Find("[data-testid='palette-input']");
        input.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        input.KeyDown(new KeyboardEventArgs { Key = "ArrowUp" });

        var results = cut.FindAll("[data-testid='palette-result']");
        Assert.Contains("is-active", results[0].GetAttribute("class") ?? "");
    }

    [Fact]
    public void Enter_Selects_Current_And_Closes()
    {
        var (ctx, palette, actions, selection) = Setup();
        var cut = ctx.RenderComponent<Palette>();
        actions.OpenPalette();
        var firstId = palette.Results[0].Id;

        cut.Find("[data-testid='palette-input']").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(firstId, selection.Selected?.Id);
        Assert.Empty(cut.FindAll("[data-testid='palette']"));
    }

    [Fact]
    public void Escape_Closes()
    {
        var (ctx, _, actions, selection) = Setup();
        var cut = ctx.RenderComponent<Palette>();
        actions.OpenPalette();

        cut.Find("[data-testid='palette-input']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll("[data-testid='palette']"));
        Assert.Null(selection.Selected);
    }

    [Fact]
    public void Click_Result_Selects_And_Closes()
    {
        var (ctx, palette, actions, selection) = Setup();
        var cut = ctx.RenderComponent<Palette>();
        actions.OpenPalette();
        var firstId = palette.Results[0].Id;

        cut.FindAll("[data-testid='palette-result']")[0].Click();

        Assert.Equal(firstId, selection.Selected?.Id);
        Assert.Empty(cut.FindAll("[data-testid='palette']"));
    }

    [Fact]
    public void Click_Backdrop_Closes()
    {
        var (ctx, _, actions, _) = Setup();
        var cut = ctx.RenderComponent<Palette>();
        actions.OpenPalette();

        cut.Find("[data-testid='palette-backdrop']").Click();

        Assert.Empty(cut.FindAll("[data-testid='palette']"));
    }

    [Fact]
    public void Empty_Results_Shows_Empty_State()
    {
        var (ctx, _, actions, _) = Setup();
        var cut = ctx.RenderComponent<Palette>();
        actions.OpenPalette();

        cut.Find("[data-testid='palette-input']").Input("zzzzzz-no-match");

        Assert.NotNull(cut.Find("[data-testid='palette-empty']"));
    }

    [Fact]
    public void Palette_Input_Has_Combobox_Role_And_Aria_Label()
    {
        var (ctx, _, actions, _) = Setup();
        var cut = ctx.RenderComponent<Palette>();
        actions.OpenPalette();

        var input = cut.Find("[data-testid='palette-input']");
        Assert.Equal("combobox", input.GetAttribute("role"));
        Assert.Equal("Search steps", input.GetAttribute("aria-label"));
    }

    [Fact]
    public void Palette_Results_Have_Listbox_Role()
    {
        var (ctx, _, actions, _) = Setup();
        var cut = ctx.RenderComponent<Palette>();
        actions.OpenPalette();

        var results = cut.Find("[data-testid='palette-results']");
        Assert.Equal("listbox", results.GetAttribute("role"));
    }

    [Fact]
    public void Palette_Result_Has_Option_Role_And_AriaSelected_Reflects_State()
    {
        var (ctx, _, actions, _) = Setup();
        var cut = ctx.RenderComponent<Palette>();
        actions.OpenPalette();

        var results = cut.FindAll("[data-testid='palette-result']");
        Assert.Equal("option", results[0].GetAttribute("role"));
        Assert.Equal("true", results[0].GetAttribute("aria-selected"));
        Assert.Equal("false", results[1].GetAttribute("aria-selected"));
    }

    [Fact]
    public void Palette_Input_AriaActiveDescendant_Points_To_Selected_Result()
    {
        var (ctx, palette, actions, _) = Setup();
        var cut = ctx.RenderComponent<Palette>();
        actions.OpenPalette();

        var input = cut.Find("[data-testid='palette-input']");
        var results = cut.FindAll("[data-testid='palette-result']");
        Assert.Equal(results[0].GetAttribute("id"), input.GetAttribute("aria-activedescendant"));

        palette.MoveSelectionDown();

        input = cut.Find("[data-testid='palette-input']");
        results = cut.FindAll("[data-testid='palette-result']");
        Assert.Equal(results[1].GetAttribute("id"), input.GetAttribute("aria-activedescendant"));
    }

    [Fact]
    public void Active_Result_Has_Is_Active_Class()
    {
        var (ctx, _, actions, _) = Setup();
        var cut = ctx.RenderComponent<Palette>();
        actions.OpenPalette();

        var first = cut.FindAll("[data-testid='palette-result']")[0];
        Assert.Contains("is-active", first.GetAttribute("class") ?? "");
    }
}
