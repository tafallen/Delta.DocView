using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Delta.DocView.Tests.Components;

public class RelatedStepsTests
{
    private static Step S(string id, string pattern = "the system is ready", string type = "Given",
        IReadOnlyList<string>? suggestsNext = null)
        => new()
        {
            Id = id,
            Type = type,
            Pattern = pattern,
            File = "f.feature",
            Line = 1,
            Domain = "auth",
            SuggestsNext = suggestsNext ?? Array.Empty<string>(),
        };

    private static (TestContext ctx, ClientStepLibraryStore store, SelectionState sel) Setup(IEnumerable<Step> steps)
    {
        var ctx = new TestContext();
        ctx.Services.AddScoped<ClientStepLibraryStore>();
        ctx.Services.AddScoped<SelectionState>();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var store = ctx.Services.GetRequiredService<ClientStepLibraryStore>();
        store.Populate(new StepLibrary
        {
            Steps = steps.ToList(),
            Domains = new List<StepDomain> { new() { Id = "auth", Label = "Auth" } },
        });
        var sel = ctx.Services.GetRequiredService<SelectionState>();
        return (ctx, store, sel);
    }

    [Fact]
    public void Empty_SuggestsNext_Renders_Nothing()
    {
        var step = S("root", suggestsNext: Array.Empty<string>());
        var (ctx, _, _) = Setup(new[] { step });

        var cut = ctx.RenderComponent<RelatedSteps>(p => p.Add(c => c.Step, step));

        Assert.Equal(string.Empty, cut.Markup.Trim());
    }

    [Fact]
    public void Renders_Up_To_Four_Cards()
    {
        var ids = new[] { "a", "b", "c", "d", "e", "f" };
        var steps = ids.Select(i => S(i, pattern: $"pattern {i}")).ToList();
        var root = S("root", suggestsNext: ids);
        steps.Add(root);

        var (ctx, _, _) = Setup(steps);
        var cut = ctx.RenderComponent<RelatedSteps>(p => p.Add(c => c.Step, root));

        Assert.Equal(4, cut.FindAll("[data-testid='related-card']").Count);
    }

    [Fact]
    public void Unresolvable_Ids_Silently_Skipped()
    {
        var steps = new List<Step>
        {
            S("a", pattern: "pa"),
            S("b", pattern: "pb"),
            S("c", pattern: "pc"),
        };
        var root = S("root", suggestsNext: new[] { "a", "missing", "b", "alsoMissing", "c" });
        steps.Add(root);

        var (ctx, _, _) = Setup(steps);
        var cut = ctx.RenderComponent<RelatedSteps>(p => p.Add(c => c.Step, root));

        var cards = cut.FindAll("[data-testid='related-card']");
        Assert.Equal(3, cards.Count);
        Assert.Equal("a", cards[0].GetAttribute("data-step-id"));
        Assert.Equal("b", cards[1].GetAttribute("data-step-id"));
        Assert.Equal("c", cards[2].GetAttribute("data-step-id"));
    }

    [Fact]
    public void Cards_Have_Type_Chip_Matching_Related_Step_Type()
    {
        var given = S("g1", pattern: "given step", type: "Given");
        var root = S("root", suggestsNext: new[] { "g1" });
        var (ctx, _, _) = Setup(new[] { given, root });

        var cut = ctx.RenderComponent<RelatedSteps>(p => p.Add(c => c.Step, root));

        var card = cut.Find("[data-testid='related-card']");
        var chip = card.QuerySelector(".type-chip.chip-given");
        Assert.NotNull(chip);
        Assert.Equal("Given", chip!.TextContent);
    }

    [Fact]
    public void Long_Pattern_Truncated_With_Ellipsis()
    {
        var longPattern = new string('x', 80);
        var related = S("l1", pattern: longPattern);
        var root = S("root", suggestsNext: new[] { "l1" });
        var (ctx, _, _) = Setup(new[] { related, root });

        var cut = ctx.RenderComponent<RelatedSteps>(p => p.Add(c => c.Step, root));

        var text = cut.Find(".related-pattern").TextContent;
        Assert.EndsWith("…", text);
        Assert.Equal(60, text.Length);
    }

    [Fact]
    public void Card_Aria_Label_Contains_Full_Pattern()
    {
        var longPattern = new string('x', 80);
        var related = S("l1", pattern: longPattern);
        var root = S("root", suggestsNext: new[] { "l1" });
        var (ctx, _, _) = Setup(new[] { related, root });

        var cut = ctx.RenderComponent<RelatedSteps>(p => p.Add(c => c.Step, root));

        var card = cut.Find("[data-testid='related-card']");
        var ariaLabel = card.GetAttribute("aria-label") ?? "";
        Assert.Contains(longPattern, ariaLabel);
        Assert.DoesNotContain("…", ariaLabel);
    }

    [Fact]
    public void Card_Click_Selects_Related_Step()
    {
        var a = S("a", pattern: "pa");
        var b = S("b", pattern: "pb");
        var root = S("root", suggestsNext: new[] { "a", "b" });
        var (ctx, _, sel) = Setup(new[] { a, b, root });

        var cut = ctx.RenderComponent<RelatedSteps>(p => p.Add(c => c.Step, root));

        var firstCard = cut.FindAll("[data-testid='related-card']")[0];
        var expectedId = firstCard.GetAttribute("data-step-id");
        firstCard.Click();

        Assert.NotNull(sel.Selected);
        Assert.Equal(expectedId, sel.Selected!.Id);
    }
}
