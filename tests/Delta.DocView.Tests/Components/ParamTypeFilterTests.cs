using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Delta.DocView.Tests.Components;

public class ParamTypeFilterTests
{
    private static Step MakeStep(string id, params (string name, string type)[] ps) => new()
    {
        Id = id, Type = "Given", Pattern = "p",
        Params = ps.Select(p => new StepParam { Name = p.name, Type = p.type, Example = "" }).ToList(),
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
            MakeStep("s1", ("u", "string")),
            MakeStep("s2", ("n", "int")),
            MakeStep("s3", ("u", "string"), ("body", "DocString"))
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
    public void Renders_One_Chip_Per_Distinct_Param_Type_Ordered_Ordinal()
    {
        var (ctx, _, _) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<ParamTypeFilter>();

        var chips = cut.FindAll("button.param-chip");
        Assert.Equal(3, chips.Count);
        Assert.Equal("DocString", chips[0].GetAttribute("data-paramtype"));
        Assert.Equal("int", chips[1].GetAttribute("data-paramtype"));
        Assert.Equal("string", chips[2].GetAttribute("data-paramtype"));
    }

    [Fact]
    public void No_Chip_Active_By_Default()
    {
        var (ctx, _, _) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<ParamTypeFilter>();

        foreach (var chip in cut.FindAll("button.param-chip"))
        {
            Assert.DoesNotContain("is-active", chip.GetAttribute("class"));
        }
    }

    [Fact]
    public void Clicking_Chip_Adds_Param_Type_And_Marks_Active()
    {
        var (ctx, _, state) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<ParamTypeFilter>();

        cut.Find("button.param-chip[data-paramtype=\"string\"]").Click();

        Assert.Contains("string", state.ParamTypes);
        var chip = cut.Find("button.param-chip[data-paramtype=\"string\"]");
        Assert.Contains("is-active", chip.GetAttribute("class"));
    }

    [Fact]
    public void Clicking_Active_Chip_Again_Removes_It()
    {
        var (ctx, _, state) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<ParamTypeFilter>();

        cut.Find("button.param-chip[data-paramtype=\"string\"]").Click();
        cut.Find("button.param-chip[data-paramtype=\"string\"]").Click();

        Assert.DoesNotContain("string", state.ParamTypes);
        var chip = cut.Find("button.param-chip[data-paramtype=\"string\"]");
        Assert.DoesNotContain("is-active", chip.GetAttribute("class"));
    }

    [Fact]
    public void Multi_Select_Activates_Multiple_Chips()
    {
        var (ctx, _, state) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<ParamTypeFilter>();

        cut.Find("button.param-chip[data-paramtype=\"string\"]").Click();
        cut.Find("button.param-chip[data-paramtype=\"int\"]").Click();

        Assert.Contains("string", state.ParamTypes);
        Assert.Contains("int", state.ParamTypes);
        Assert.Contains("is-active", cut.Find("button.param-chip[data-paramtype=\"string\"]").GetAttribute("class"));
        Assert.Contains("is-active", cut.Find("button.param-chip[data-paramtype=\"int\"]").GetAttribute("class"));
    }
}
