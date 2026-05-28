using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Delta.DocView.Tests.Components;

public class DomainFilterTests
{
    private static Step MakeStep(string id, string domain) => new()
    {
        Id = id, Type = "Given", Pattern = "p", Params = [],
        File = "f.cs", Line = 1, Domain = domain, Tags = [],
        Used = 0, Description = "", Source = "", SuggestsNext = []
    };

    private static StepLibrary BuildLibrary() => new()
    {
        Version = "1.0.0",
        GeneratedAt = "2026-01-01T00:00:00Z",
        GeneratorVersion = "1.0.0",
        Domains =
        [
            new StepDomain { Id = "Auth", Label = "Auth" },
            new StepDomain { Id = "Billing", Label = "Billing" }
        ],
        Steps =
        [
            MakeStep("a1", "Auth"), MakeStep("a2", "Auth"), MakeStep("a3", "Auth"),
            MakeStep("b1", "Billing"), MakeStep("b2", "Billing")
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
    public void Renders_All_Domains_With_Total_Count_Active_By_Default()
    {
        var (ctx, _, _) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<DomainFilter>();

        var all = cut.Find("button.domain-row[data-domain=\"\"]");
        Assert.Contains("is-active", all.GetAttribute("class"));
        Assert.Equal("5", all.QuerySelector(".count")!.TextContent);
    }

    [Fact]
    public void Renders_One_Button_Per_Domain_With_Label_And_Count()
    {
        var (ctx, _, _) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<DomainFilter>();

        var buttons = cut.FindAll("button.domain-row");
        // All + 2 domains
        Assert.Equal(3, buttons.Count);
        Assert.Equal("Auth", buttons[1].GetAttribute("data-domain"));
        Assert.Equal("Auth", buttons[1].QuerySelector(".domain-label")!.TextContent);
        Assert.Equal("3", buttons[1].QuerySelector(".count")!.TextContent);
        Assert.Equal("Billing", buttons[2].GetAttribute("data-domain"));
        Assert.Equal("Billing", buttons[2].QuerySelector(".domain-label")!.TextContent);
        Assert.Equal("2", buttons[2].QuerySelector(".count")!.TextContent);
    }

    [Fact]
    public void Per_Domain_Dot_Uses_Lowercase_Css_Var()
    {
        var (ctx, _, _) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<DomainFilter>();

        var dot = cut.Find("button.domain-row[data-domain=\"Auth\"] .domain-dot");
        Assert.Contains("var(--dom-auth)", dot.GetAttribute("style"));
    }

    [Fact]
    public void Clicking_Domain_Sets_State_And_Toggles_Active()
    {
        var (ctx, _, state) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<DomainFilter>();

        cut.Find("button.domain-row[data-domain=\"Auth\"]").Click();

        Assert.Equal("Auth", state.Domain);
        var authBtn = cut.Find("button.domain-row[data-domain=\"Auth\"]");
        Assert.Contains("is-active", authBtn.GetAttribute("class"));
        var allBtn = cut.Find("button.domain-row[data-domain=\"\"]");
        Assert.DoesNotContain("is-active", allBtn.GetAttribute("class"));
    }

    [Fact]
    public void Clicking_All_Clears_Selected_Domain()
    {
        var (ctx, _, state) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<DomainFilter>();

        cut.Find("button.domain-row[data-domain=\"Auth\"]").Click();
        Assert.Equal("Auth", state.Domain);

        cut.Find("button.domain-row[data-domain=\"\"]").Click();

        Assert.Null(state.Domain);
        var allBtn = cut.Find("button.domain-row[data-domain=\"\"]");
        Assert.Contains("is-active", allBtn.GetAttribute("class"));
    }
}
