using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;

namespace Delta.DocView.Tests.Services;

public class FilterEngineTests
{
    private static Step MakeStep(
        string id,
        string type = "Given",
        string pattern = "I am logged in",
        string domain = "Auth",
        IReadOnlyList<StepParam>? @params = null) =>
        new()
        {
            Id = id,
            Type = type,
            Pattern = pattern,
            Domain = domain,
            Params = @params ?? [],
        };

    private static List<Step> SampleSteps() =>
    [
        MakeStep("s1", "Given", "I am logged in as {string}", "Auth",
            [new StepParam { Name = "u", Type = "string" }]),
        MakeStep("s2", "When", "I click {string}", "UI",
            [new StepParam { Name = "label", Type = "string" }]),
        MakeStep("s3", "Then", "I see {int} results", "Search",
            [new StepParam { Name = "n", Type = "int" }]),
        MakeStep("s4", "And", "I wait", "Auth"),
    ];

    private static FilterState DefaultState() => new();

    [Fact]
    public void Apply_AllTypesNoOtherFilters_ReturnsAll()
    {
        var steps = SampleSteps();
        var state = DefaultState();
        var favs = new InMemoryFavouritesStore();

        var result = FilterEngine.Apply(steps, state, favs);

        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void Apply_SingleTypeSelected_ReturnsOnlyMatching()
    {
        var steps = SampleSteps();
        var state = DefaultState();
        state.ToggleType("When");
        state.ToggleType("Then");
        state.ToggleType("And");
        // Only "Given" remains
        var favs = new InMemoryFavouritesStore();

        var result = FilterEngine.Apply(steps, state, favs);

        Assert.Single(result);
        Assert.Equal("s1", result[0].Id);
    }

    [Fact]
    public void Apply_DomainSet_ReturnsOnlyThatDomain()
    {
        var steps = SampleSteps();
        var state = DefaultState();
        state.SetDomain("Auth");
        var favs = new InMemoryFavouritesStore();

        var result = FilterEngine.Apply(steps, state, favs);

        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.Equal("Auth", s.Domain));
    }

    [Fact]
    public void Apply_ParamTypeSet_ReturnsStepsWithMatchingParam()
    {
        var steps = SampleSteps();
        var state = DefaultState();
        state.ToggleParamType("int");
        var favs = new InMemoryFavouritesStore();

        var result = FilterEngine.Apply(steps, state, favs);

        Assert.Single(result);
        Assert.Equal("s3", result[0].Id);
    }

    [Fact]
    public void Apply_ParamTypeNotPresent_ReturnsEmpty()
    {
        var steps = SampleSteps();
        var state = DefaultState();
        state.ToggleParamType("guid");
        var favs = new InMemoryFavouritesStore();

        var result = FilterEngine.Apply(steps, state, favs);

        Assert.Empty(result);
    }

    [Fact]
    public void Apply_FavsOnlyWithNoFavourites_ReturnsEmpty()
    {
        var steps = SampleSteps();
        var state = DefaultState();
        state.SetFavsOnly(true);
        var favs = new InMemoryFavouritesStore();

        var result = FilterEngine.Apply(steps, state, favs);

        Assert.Empty(result);
    }

    [Fact]
    public void Apply_FavsOnlyWithOneFavourite_ReturnsThatStep()
    {
        var steps = SampleSteps();
        var state = DefaultState();
        state.SetFavsOnly(true);
        var favs = new InMemoryFavouritesStore();
        favs.Toggle("s2");

        var result = FilterEngine.Apply(steps, state, favs);

        Assert.Single(result);
        Assert.Equal("s2", result[0].Id);
    }

    [Fact]
    public void Apply_QuerySubstringCaseInsensitive_MatchesPattern()
    {
        var steps = SampleSteps();
        var state = DefaultState();
        state.SetQuery("LOG");
        var favs = new InMemoryFavouritesStore();

        var result = FilterEngine.Apply(steps, state, favs);

        Assert.Single(result);
        Assert.Equal("s1", result[0].Id);
    }

    [Fact]
    public void Apply_CombinedFilters_NarrowToSingleStep()
    {
        var steps = SampleSteps();
        var state = DefaultState();
        // keep only Given
        state.ToggleType("When");
        state.ToggleType("Then");
        state.ToggleType("And");
        state.SetDomain("Auth");
        state.SetQuery("logged");
        var favs = new InMemoryFavouritesStore();

        var result = FilterEngine.Apply(steps, state, favs);

        Assert.Single(result);
        Assert.Equal("s1", result[0].Id);
    }
}
