using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Delta.DocView.Tests.Components;

public class StepTypeFilterTests
{
    private static Step MakeStep(string id, string type) => new()
    {
        Id = id, Type = type, Pattern = "p", Params = [],
        File = "f.cs", Line = 1, Domain = "Auth", Tags = [],
        Used = 0, Description = "", Source = "", SuggestsNext = []
    };

    private static StepLibrary BuildLibrary() => new()
    {
        Version = "1.0.0",
        GeneratedAt = "2026-01-01T00:00:00Z",
        GeneratorVersion = "1.0.0",
        Domains = [new StepDomain { Id = "Auth", Label = "Auth" }],
        Steps =
        [
            MakeStep("g1", "Given"), MakeStep("g2", "Given"), MakeStep("g3", "Given"),
            MakeStep("w1", "When"), MakeStep("w2", "When"),
            MakeStep("t1", "Then")
        ],
        Signature = new StepSignature { Algorithm = "SHA-256", Digest = new string('0', 64) }
    };

    private static (TestContext ctx, ClientStepLibraryStore store, FilterState state) Setup()
    {
        var ctx = new TestContext();
        ctx.Services.AddScoped<ClientStepLibraryStore>();
        ctx.Services.AddScoped<FilterState>();
        ctx.Services.AddScoped<IFavouritesStore, InMemoryFavouritesStore>();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var store = ctx.Services.GetRequiredService<ClientStepLibraryStore>();
        store.Populate(BuildLibrary());
        var state = ctx.Services.GetRequiredService<FilterState>();
        return (ctx, store, state);
    }

    [Fact]
    public void Renders_Three_Buttons_In_Order()
    {
        var (ctx, _, _) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<StepTypeFilter>();

        var buttons = cut.FindAll("button.step-type");
        Assert.Equal(3, buttons.Count);
        Assert.Equal("Given", buttons[0].GetAttribute("data-type"));
        Assert.Equal("When", buttons[1].GetAttribute("data-type"));
        Assert.Equal("Then", buttons[2].GetAttribute("data-type"));
    }

    [Fact]
    public void Each_Button_Shows_Correct_Count_From_Library()
    {
        var (ctx, _, _) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<StepTypeFilter>();

        var buttons = cut.FindAll("button.step-type");
        Assert.Equal("3", buttons[0].QuerySelector(".count")!.TextContent);
        Assert.Equal("2", buttons[1].QuerySelector(".count")!.TextContent);
        Assert.Equal("1", buttons[2].QuerySelector(".count")!.TextContent);
    }

    [Fact]
    public void All_Three_Are_Active_By_Default()
    {
        var (ctx, _, _) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<StepTypeFilter>();

        foreach (var btn in cut.FindAll("button.step-type"))
        {
            Assert.Contains("is-active", btn.GetAttribute("class"));
        }
    }

    [Fact]
    public void Clicking_Active_Button_Toggles_Off()
    {
        var (ctx, _, state) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<StepTypeFilter>();

        cut.Find("button.step-type[data-type=\"Given\"]").Click();

        Assert.DoesNotContain("Given", state.Types);
        var givenBtn = cut.Find("button.step-type[data-type=\"Given\"]");
        Assert.DoesNotContain("is-active", givenBtn.GetAttribute("class"));
    }

    [Fact]
    public void Clicking_Last_Active_Button_Is_NoOp()
    {
        var (ctx, _, state) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<StepTypeFilter>();

        // Deselect Given and When — leave only Then active.
        cut.Find("button.step-type[data-type=\"Given\"]").Click();
        cut.Find("button.step-type[data-type=\"When\"]").Click();

        Assert.Single(state.Types);
        Assert.Contains("Then", state.Types);

        // Click last remaining — must be a no-op.
        cut.Find("button.step-type[data-type=\"Then\"]").Click();

        Assert.Single(state.Types);
        Assert.Contains("Then", state.Types);
        var thenBtn = cut.Find("button.step-type[data-type=\"Then\"]");
        Assert.Contains("is-active", thenBtn.GetAttribute("class"));
    }
}
