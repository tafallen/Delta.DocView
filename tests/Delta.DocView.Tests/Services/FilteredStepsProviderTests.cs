using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;

namespace Delta.DocView.Tests.Services;

public class FilteredStepsProviderTests
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

    private static (ClientStepLibraryStore store, FilterState state, IFavouritesStore favs) MakeServices()
    {
        var store = new ClientStepLibraryStore();
        store.Populate(new StepLibrary
        {
            Steps =
            [
                S("step-1", "user logs in", used: 10, type: "Given"),
                S("step-2", "user clicks login", used: 5, type: "When"),
                S("step-3", "user sees dashboard", used: 3, type: "Then"),
            ],
            Domains = [D("auth", "Auth")],
        });
        return (store, new FilterState(), new InMemoryFavouritesStore());
    }

    [Fact]
    public void Initial_Filtered_Matches_Engine_Output()
    {
        var (store, state, favs) = MakeServices();
        using var provider = new FilteredStepsProvider(store, state, favs);

        var expected = StepRanking.Rank(FilterEngine.Apply(store.Steps, state, favs));
        Assert.Equal(expected.Select(s => s.Id), provider.Filtered.Select(s => s.Id));
    }

    [Fact]
    public void FilterState_Changed_Triggers_Recompute_And_Raises_Changed()
    {
        var (store, state, favs) = MakeServices();
        using var provider = new FilteredStepsProvider(store, state, favs);
        var raised = 0;
        provider.Changed += () => raised++;

        state.ToggleType("When");

        Assert.Equal(1, raised);
        Assert.DoesNotContain(provider.Filtered, s => s.Type == "When");
    }

    [Fact]
    public void Favourites_Changed_Triggers_Recompute_And_Raises_Changed()
    {
        var (store, state, favs) = MakeServices();
        using var provider = new FilteredStepsProvider(store, state, favs);
        var raised = 0;
        provider.Changed += () => raised++;

        favs.Toggle("step-1");

        Assert.Equal(1, raised);
        Assert.True(favs.Has("step-1"));
    }

    [Fact]
    public void Disposal_Unsubscribes_From_FilterState_And_Favs()
    {
        var (store, state, favs) = MakeServices();
        var provider = new FilteredStepsProvider(store, state, favs);
        var raised = 0;
        provider.Changed += () => raised++;

        provider.Dispose();

        state.ToggleType("When");
        favs.Toggle("step-1");

        Assert.Equal(0, raised);
    }
}
