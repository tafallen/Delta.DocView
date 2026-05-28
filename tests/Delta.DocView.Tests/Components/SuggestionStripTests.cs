using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Delta.DocView.Tests.Components;

public class SuggestionStripTests : TestContext
{
    private static Step MakeStep(string id, string type = "Given",
        IReadOnlyList<string>? suggestsNext = null) => new()
    {
        Id = id, Type = type, Pattern = $"pattern {id}",
        Domain = "Auth", File = "T.cs", Line = 1,
        SuggestsNext = suggestsNext ?? []
    };

    private (ComposerState composer, ClientStepLibraryStore store) Setup(
        IEnumerable<Step> allSteps, IEnumerable<Step> composerSteps)
    {
        Services.AddScoped<ClientStepLibraryStore>();
        Services.AddScoped<FilterState>();
        Services.AddScoped<IFavouritesStore, InMemoryFavouritesStore>();
        Services.AddScoped<FilteredStepsProvider>();
        Services.AddScoped<IKeyboardActions, KeyboardActions>();
        Services.AddScoped<SelectionState>();
        Services.AddScoped<ComposerState>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        var store = Services.GetRequiredService<ClientStepLibraryStore>();
        store.Populate(new StepLibrary
        {
            Steps = allSteps.ToList(),
            Domains = [new StepDomain { Id = "Auth", Label = "Auth" }]
        });

        var composer = Services.GetRequiredService<ComposerState>();
        foreach (var s in composerSteps) composer.AddStep(s);
        composer.Toggle();

        return (composer, store);
    }

    [Fact]
    public void SuggestionStrip_NoStepsInComposer_RendersNothing()
    {
        Services.AddScoped<ClientStepLibraryStore>();
        Services.AddScoped<FilterState>();
        Services.AddScoped<IFavouritesStore, InMemoryFavouritesStore>();
        Services.AddScoped<FilteredStepsProvider>();
        Services.AddScoped<IKeyboardActions, KeyboardActions>();
        Services.AddScoped<SelectionState>();
        Services.AddScoped<ComposerState>();
        JSInterop.Mode = JSRuntimeMode.Loose;
        var store = Services.GetRequiredService<ClientStepLibraryStore>();
        store.Populate(new StepLibrary { Steps = [], Domains = [] });

        var cut = RenderComponent<SuggestionStrip>();
        Assert.Empty(cut.FindAll(".suggestion-chip"));
    }

    [Fact]
    public void SuggestionStrip_ShowsChipsForSuggestsNext()
    {
        var s1 = MakeStep("s1", suggestsNext: ["s2", "s3"]);
        var s2 = MakeStep("s2");
        var s3 = MakeStep("s3");
        Setup([s1, s2, s3], [s1]);

        var cut = RenderComponent<SuggestionStrip>();
        Assert.Equal(2, cut.FindAll(".suggestion-chip").Count);
    }

    [Fact]
    public void SuggestionStrip_ExcludesAlreadyAddedSteps()
    {
        var s1 = MakeStep("s1", suggestsNext: ["s2", "s3"]);
        var s2 = MakeStep("s2", suggestsNext: ["s3"]);
        var s3 = MakeStep("s3");
        var (composer, _) = Setup([s1, s2, s3], [s1, s2]); // s1 and s2 in composer, s2 is last

        var cut = RenderComponent<SuggestionStrip>();
        // s2 suggests only s3, and s3 is not in composer yet, so show 1 chip
        Assert.Single(cut.FindAll(".suggestion-chip")); // only s3
    }

    [Fact]
    public void SuggestionStrip_CapsAtFourChips()
    {
        var suggestions = Enumerable.Range(2, 6).Select(i => $"s{i}").ToList();
        var s1 = MakeStep("s1", suggestsNext: suggestions.ToArray());
        var allSteps = new[] { s1 }.Concat(suggestions.Select(id => MakeStep(id))).ToList();
        Setup(allSteps, [s1]);

        var cut = RenderComponent<SuggestionStrip>();
        Assert.Equal(4, cut.FindAll(".suggestion-chip").Count);
    }

    [Fact]
    public void SuggestionStrip_ChipClick_AddsStepToComposer()
    {
        var s1 = MakeStep("s1", suggestsNext: ["s2"]);
        var s2 = MakeStep("s2");
        var (composer, _) = Setup([s1, s2], [s1]);

        var cut = RenderComponent<SuggestionStrip>();
        cut.Find(".suggestion-chip").Click();

        Assert.Equal(2, composer.StepCount);
        Assert.Contains(composer.Steps, x => x.Step.Id == "s2");
    }
}
