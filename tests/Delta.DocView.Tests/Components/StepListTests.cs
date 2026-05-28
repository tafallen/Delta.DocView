using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Delta.DocView.Tests.Components;

public class StepListTests
{
    private static Step S(string id, string pattern, int used = 1, string type = "Given", string domain = "auth")
        => new()
        {
            Id = id,
            Type = type,
            Pattern = pattern,
            File = "f.feature",
            Line = 1,
            Domain = domain,
            Used = used,
        };

    private static StepDomain D(string id, string label) => new() { Id = id, Label = label };

    private static StepLibrary Library(IEnumerable<Step> steps, IEnumerable<StepDomain>? domains = null)
        => new()
        {
            Steps = steps.ToList(),
            Domains = (domains ?? new[] { D("auth", "Auth"), D("billing", "Billing") }).ToList(),
        };

    private static (TestContext ctx, ClientStepLibraryStore store, FilterState state, IFavouritesStore favs) Setup(StepLibrary library)
    {
        var ctx = new TestContext();
        ctx.Services.AddScoped<ClientStepLibraryStore>();
        ctx.Services.AddScoped<FilterState>();
        ctx.Services.AddScoped<IFavouritesStore, InMemoryFavouritesStore>();
        ctx.Services.AddScoped<SelectionState>();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var store = ctx.Services.GetRequiredService<ClientStepLibraryStore>();
        store.Populate(library);
        var state = ctx.Services.GetRequiredService<FilterState>();
        var favs = ctx.Services.GetRequiredService<IFavouritesStore>();
        return (ctx, store, state, favs);
    }

    [Fact]
    public void Header_Shows_Count_Of_Total_With_Default_Filters()
    {
        var lib = Library(new[]
        {
            S("s1", "user logs in", used: 100),
            S("s2", "user logs out", used: 50),
            S("s3", "user views profile", used: 10),
            S("s4", "user edits profile", used: 1),
        });
        var (ctx, _, _, _) = Setup(lib);
        using var _c = ctx;

        var cut = ctx.RenderComponent<StepList>();
        var header = cut.Find("[data-testid='step-list-count']").TextContent.Trim();
        Assert.Equal("4 of 4 matching", header);
    }

    [Fact]
    public void Header_Includes_Query_With_Two_Matches()
    {
        var lib = Library(new[]
        {
            S("s1", "user login form", used: 100),
            S("s2", "user login submit", used: 50),
            S("s3", "user views profile", used: 10),
            S("s4", "user edits profile", used: 1),
        });
        var (ctx, _, state, _) = Setup(lib);
        using var _c = ctx;

        var cut = ctx.RenderComponent<StepList>();
        state.SetQuery("login");

        var header = cut.Find("[data-testid='step-list-count']").TextContent.Trim();
        Assert.Equal("2 of 4 matching for \"login\"", header);
    }

    [Fact]
    public void Empty_Result_Renders_EmptyState_And_No_StepRows()
    {
        var lib = Library(new[]
        {
            S("s1", "alpha", used: 100, type: "Given"),
        });
        var (ctx, _, state, _) = Setup(lib);
        using var _c = ctx;

        var cut = ctx.RenderComponent<StepList>();
        state.SetQuery("zzzzz-no-match");

        Assert.NotNull(cut.Find("[data-testid='step-list-empty']"));
        Assert.Empty(cut.FindAll(".step-row"));
    }

    [Fact]
    public void Renders_EmptyLibrary_Card_When_Store_Has_No_Steps()
    {
        var ctx = new TestContext();
        ctx.Services.AddScoped<ClientStepLibraryStore>();
        ctx.Services.AddScoped<FilterState>();
        ctx.Services.AddScoped<IFavouritesStore, InMemoryFavouritesStore>();
        ctx.Services.AddScoped<SelectionState>();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        using var _c = ctx;

        var cut = ctx.RenderComponent<StepList>();

        Assert.NotNull(cut.Find("[data-testid='step-list-empty-library']"));
        Assert.Empty(cut.FindAll("[data-testid='step-list-empty']"));
        Assert.Empty(cut.FindAll(".step-row"));
    }

    [Fact]
    public void Domain_Set_Renders_Flat_Without_Group_Headers()
    {
        var lib = Library(new[]
        {
            S("s1", "alpha", used: 10, domain: "auth"),
            S("s2", "beta", used: 5, domain: "auth"),
        });
        var (ctx, _, state, _) = Setup(lib);
        using var _c = ctx;

        var cut = ctx.RenderComponent<StepList>();
        state.SetDomain("auth");

        Assert.Empty(cut.FindAll(".step-group-header"));
        Assert.Equal(2, cut.FindAll(".step-row").Count);
    }

    [Fact]
    public void Domain_Null_Renders_Group_Headers()
    {
        var lib = Library(new[]
        {
            S("s1", "alpha", used: 10, domain: "auth"),
            S("s2", "beta", used: 5, domain: "billing"),
        });
        var (ctx, _, _, _) = Setup(lib);
        using var _c = ctx;

        var cut = ctx.RenderComponent<StepList>();

        Assert.NotEmpty(cut.FindAll(".step-group-header"));
    }

    [Fact]
    public void Sort_By_Used_Descending()
    {
        var lib = Library(new[]
        {
            S("sa", "A pattern", used: 5, domain: "auth"),
            S("sb", "B pattern", used: 10, domain: "auth"),
        });
        var (ctx, _, state, _) = Setup(lib);
        using var _c = ctx;

        // Pick a single domain so output is flat (no group headers reordering)
        var cut = ctx.RenderComponent<StepList>();
        state.SetDomain("auth");

        var rows = cut.FindAll(".step-row");
        Assert.Equal(2, rows.Count);
        Assert.Equal("sb", rows[0].GetAttribute("data-step-id"));
        Assert.Equal("sa", rows[1].GetAttribute("data-step-id"));
    }

    [Fact]
    public void Sort_TieBreak_By_Pattern_Ordinal_Ascending()
    {
        var lib = Library(new[]
        {
            S("sb", "banana", used: 7, domain: "auth"),
            S("sa", "apple", used: 7, domain: "auth"),
        });
        var (ctx, _, state, _) = Setup(lib);
        using var _c = ctx;

        var cut = ctx.RenderComponent<StepList>();
        state.SetDomain("auth");

        var rows = cut.FindAll(".step-row");
        Assert.Equal(2, rows.Count);
        Assert.Equal("sa", rows[0].GetAttribute("data-step-id"));
        Assert.Equal("sb", rows[1].GetAttribute("data-step-id"));
    }
}
