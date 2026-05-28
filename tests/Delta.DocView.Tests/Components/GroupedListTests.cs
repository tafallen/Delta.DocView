using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Delta.DocView.Tests.Components;

public class GroupedListTests
{
    private static (TestContext ctx, SelectionState sel) Setup()
    {
        var ctx = new TestContext();
        ctx.Services.AddScoped<IFavouritesStore, InMemoryFavouritesStore>();
        ctx.Services.AddScoped<SelectionState>();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var sel = ctx.Services.GetRequiredService<SelectionState>();
        return (ctx, sel);
    }

    private static Step S(string id, string domain = "auth", string type = "Given")
        => new() { Id = id, Type = type, Pattern = "p " + id, File = "f.feature", Line = 1, Domain = domain };

    private static StepDomain D(string id, string label) => new() { Id = id, Label = label };

    [Fact]
    public void Flat_Renders_Rows_In_Order_With_No_Headers()
    {
        var (ctx, _) = Setup();
        using var _c = ctx;
        var steps = new[] { S("a"), S("b"), S("c") };
        var cut = ctx.RenderComponent<GroupedList>(p => p
            .Add(x => x.Steps, steps)
            .Add(x => x.Domains, Array.Empty<StepDomain>())
            .Add(x => x.Grouped, false));

        Assert.Empty(cut.FindAll(".step-group-header"));
        var rows = cut.FindAll(".step-row");
        Assert.Equal(3, rows.Count);
        Assert.Equal("a", rows[0].GetAttribute("data-step-id"));
        Assert.Equal("b", rows[1].GetAttribute("data-step-id"));
        Assert.Equal("c", rows[2].GetAttribute("data-step-id"));
    }

    [Fact]
    public void Grouped_Orders_By_Descending_Count()
    {
        var (ctx, _) = Setup();
        using var _c = ctx;
        var steps = new[]
        {
            S("b1", "billing"),
            S("a1", "auth"), S("a2", "auth"), S("a3", "auth"),
        };
        var domains = new[] { D("auth", "Auth & Identity"), D("billing", "Billing") };
        var cut = ctx.RenderComponent<GroupedList>(p => p
            .Add(x => x.Steps, steps)
            .Add(x => x.Domains, domains)
            .Add(x => x.Grouped, true));

        var headers = cut.FindAll(".step-group-header");
        Assert.Equal(2, headers.Count);
        Assert.Equal("auth", headers[0].GetAttribute("data-domain-id"));
        Assert.Equal("billing", headers[1].GetAttribute("data-domain-id"));
    }

    [Fact]
    public void Header_Label_Resolved_From_Domains_With_Fallback_To_Id()
    {
        var (ctx, _) = Setup();
        using var _c = ctx;
        var steps = new[] { S("a1", "auth"), S("u1", "unknown") };
        var domains = new[] { D("auth", "Auth & Identity") };
        var cut = ctx.RenderComponent<GroupedList>(p => p
            .Add(x => x.Steps, steps)
            .Add(x => x.Domains, domains)
            .Add(x => x.Grouped, true));

        var headers = cut.FindAll(".step-group-header");
        var labels = headers.Select(h => h.QuerySelector(".domain-label")!.TextContent).ToList();
        Assert.Contains("Auth & Identity", labels);
        Assert.Contains("unknown", labels);
    }

    [Fact]
    public void Header_Shows_InGroup_Count()
    {
        var (ctx, _) = Setup();
        using var _c = ctx;
        var steps = new[] { S("a1", "auth"), S("a2", "auth"), S("b1", "billing") };
        var domains = new[] { D("auth", "Auth"), D("billing", "Billing") };
        var cut = ctx.RenderComponent<GroupedList>(p => p
            .Add(x => x.Steps, steps)
            .Add(x => x.Domains, domains)
            .Add(x => x.Grouped, true));

        var headers = cut.FindAll(".step-group-header");
        Assert.Equal("2", headers[0].QuerySelector(".count")!.TextContent);
        Assert.Equal("1", headers[1].QuerySelector(".count")!.TextContent);
    }

    [Fact]
    public void TieBreak_Is_Alphabetical_By_Domain_Id()
    {
        var (ctx, _) = Setup();
        using var _c = ctx;
        var steps = new[]
        {
            S("b1", "beta"), S("b2", "beta"),
            S("a1", "alpha"), S("a2", "alpha"),
        };
        var domains = new[] { D("alpha", "Alpha"), D("beta", "Beta") };
        var cut = ctx.RenderComponent<GroupedList>(p => p
            .Add(x => x.Steps, steps)
            .Add(x => x.Domains, domains)
            .Add(x => x.Grouped, true));

        var headers = cut.FindAll(".step-group-header");
        Assert.Equal("alpha", headers[0].GetAttribute("data-domain-id"));
        Assert.Equal("beta", headers[1].GetAttribute("data-domain-id"));
    }

    [Fact]
    public void Empty_Steps_Renders_Nothing()
    {
        var (ctx, _) = Setup();
        using var _c = ctx;
        var cut = ctx.RenderComponent<GroupedList>(p => p
            .Add(x => x.Steps, Array.Empty<Step>())
            .Add(x => x.Domains, Array.Empty<StepDomain>())
            .Add(x => x.Grouped, true));

        Assert.Equal(string.Empty, cut.Markup.Trim());
    }

    [Fact]
    public void External_Selection_Change_Updates_IsSelected_Class()
    {
        var (ctx, sel) = Setup();
        using var _c = ctx;
        var s1 = S("s1");
        var s2 = S("s2");
        sel.Select(s1);
        var cut = ctx.RenderComponent<GroupedList>(p => p
            .Add(x => x.Steps, new[] { s1, s2 })
            .Add(x => x.Domains, Array.Empty<StepDomain>())
            .Add(x => x.Grouped, false));

        var rows = cut.FindAll(".step-row");
        Assert.Contains("is-selected", rows[0].GetAttribute("class"));
        Assert.DoesNotContain("is-selected", rows[1].GetAttribute("class") ?? "");

        sel.Select(s2);

        rows = cut.FindAll(".step-row");
        Assert.DoesNotContain("is-selected", rows[0].GetAttribute("class") ?? "");
        Assert.Contains("is-selected", rows[1].GetAttribute("class"));
    }
}
