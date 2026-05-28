using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;

namespace Delta.DocView.Tests.Services;

public class PaletteStateTests
{
    private static Step S(
        string id,
        string pattern,
        int used = 1,
        string type = "Given",
        string domain = "auth",
        IReadOnlyList<string>? tags = null,
        IReadOnlyList<StepParam>? @params = null)
        => new()
        {
            Id = id,
            Type = type,
            Pattern = pattern,
            File = "f.feature",
            Line = 1,
            Domain = domain,
            Used = used,
            Tags = tags ?? Array.Empty<string>(),
            Params = @params ?? Array.Empty<StepParam>(),
        };

    private static StepLibrary BuildLibrary(IEnumerable<Step> steps)
        => new()
        {
            Steps = steps.ToList(),
            Domains =
            [
                new StepDomain { Id = "auth", Label = "Auth" },
                new StepDomain { Id = "billing", Label = "Billing" },
            ],
        };

    private static IEnumerable<Step> DefaultSteps()
    {
        yield return S("s1", "a user logs in", used: 50);
        yield return S("s2", "b user clicks login", used: 40);
        yield return S("s3", "c user sees dashboard", used: 30);
        yield return S("s4", "d user logs out", used: 20);
        yield return S("s5", "e user sees error", used: 10);
    }

    private static (PaletteState state, ClientStepLibraryStore store, IKeyboardActions actions, SelectionState selection)
        BuildState(IEnumerable<Step>? steps = null)
    {
        var store = new ClientStepLibraryStore();
        store.Populate(BuildLibrary(steps ?? DefaultSteps()));
        var selection = new SelectionState();
        var favs = new InMemoryFavouritesStore();
        var filterState = new FilterState();
        var provider = new FilteredStepsProvider(store, filterState, favs);
        var actions = new KeyboardActions(selection, favs, provider);
        var state = new PaletteState(store, actions, selection);
        return (state, store, actions, selection);
    }

    [Fact]
    public void Open_From_OpenPaletteRequested_Opens_With_Default_Results()
    {
        var (state, _, actions, _) = BuildState();

        actions.OpenPalette();

        Assert.True(state.IsOpen);
        Assert.True(state.Results.Count > 0);
        Assert.True(state.Results.Count <= 50);
    }

    [Fact]
    public void Close_From_CloseOverlayRequested_Closes()
    {
        var (state, _, actions, _) = BuildState();
        actions.OpenPalette();

        actions.CloseOverlay();

        Assert.False(state.IsOpen);
        Assert.Equal("", state.Query);
        Assert.Empty(state.Results);
    }

    [Fact]
    public void Default_Results_Top50_By_Used_Desc()
    {
        var steps = Enumerable.Range(1, 60)
            .Select(i => S($"id-{i}", $"step {i}", used: i))
            .ToList();
        var (state, _, _, _) = BuildState(steps);

        state.Open();

        Assert.Equal(50, state.Results.Count);
        Assert.Equal(60, state.Results[0].Used);
        Assert.Equal(59, state.Results[1].Used);
    }

    [Fact]
    public void SetQuery_NonEmpty_Filters_By_Fuzzy_Score()
    {
        var steps = new[]
        {
            S("s1", "user logs in"),
            S("s2", "user clicks button"),
            S("s3", "the dashboard appears"),
        };
        var (state, _, _, _) = BuildState(steps);
        state.Open();

        state.SetQuery("dashboard");

        Assert.Contains(state.Results, r => r.Id == "s3");
        Assert.DoesNotContain(state.Results, r => r.Id == "s1");
        Assert.DoesNotContain(state.Results, r => r.Id == "s2");
    }

    [Fact]
    public void SetQuery_NoMatch_ResultsEmpty()
    {
        var (state, _, _, _) = BuildState();
        state.Open();

        state.SetQuery("xyz123");

        Assert.Empty(state.Results);
    }

    [Fact]
    public void MoveSelectionDown_ClampsAtLast()
    {
        var (state, _, _, _) = BuildState();
        state.Open();
        var n = state.Results.Count;

        for (var i = 0; i < n; i++) state.MoveSelectionDown();
        state.MoveSelectionDown();

        Assert.Equal(n - 1, state.SelectedIndex);
    }

    [Fact]
    public void MoveSelectionUp_ClampsAtFirst()
    {
        var (state, _, _, _) = BuildState();
        state.Open();

        state.MoveSelectionUp();

        Assert.Equal(0, state.SelectedIndex);
    }

    [Fact]
    public void SetSelectedIndex_OutOfRange_NoChange()
    {
        var (state, _, _, _) = BuildState();
        state.Open();

        state.SetSelectedIndex(-1);
        Assert.Equal(0, state.SelectedIndex);

        state.SetSelectedIndex(state.Results.Count);
        Assert.Equal(0, state.SelectedIndex);
    }

    [Fact]
    public void SelectCurrent_Writes_To_SelectionState_And_Closes()
    {
        var (state, _, _, selection) = BuildState();
        state.Open();
        state.SetSelectedIndex(2);
        var picked = state.Results[2];

        state.SelectCurrent();

        Assert.NotNull(selection.Selected);
        Assert.Equal(picked.Id, selection.Selected!.Id);
        Assert.False(state.IsOpen);
    }

    [Fact]
    public void SelectCurrent_NoResults_NoOp()
    {
        var (state, _, _, selection) = BuildState(Array.Empty<Step>());
        state.Open();

        state.SelectCurrent();

        Assert.Null(selection.Selected);
        Assert.True(state.IsOpen);
    }

    [Fact]
    public void Changed_Raised_On_State_Transitions()
    {
        var (state, _, _, _) = BuildState();
        var count = 0;
        state.Changed += () => count++;

        state.Open();
        Assert.Equal(1, count);

        state.SetQuery("user");
        Assert.Equal(2, count);

        state.MoveSelectionDown();
        Assert.Equal(3, count);

        state.Close();
        Assert.Equal(4, count);
    }

    [Fact]
    public void SetQuery_SameValue_DoesNot_Raise_Changed()
    {
        var (state, _, _, _) = BuildState();
        state.Open();
        state.SetQuery("abc");
        var count = 0;
        state.Changed += () => count++;

        state.SetQuery("abc");

        Assert.Equal(0, count);
    }

    [Fact]
    public void Open_When_Already_Open_NoOp()
    {
        var (state, _, _, _) = BuildState();
        state.Open();
        state.SetQuery("user");
        state.SetSelectedIndex(1);
        var savedQuery = state.Query;
        var savedIndex = state.SelectedIndex;
        var count = 0;
        state.Changed += () => count++;

        state.Open();

        Assert.Equal(0, count);
        Assert.Equal(savedQuery, state.Query);
        Assert.Equal(savedIndex, state.SelectedIndex);
    }

    [Fact]
    public void Disposal_Unsubscribes_From_Actions()
    {
        var (state, _, actions, _) = BuildState();

        state.Dispose();
        actions.OpenPalette();

        Assert.False(state.IsOpen);
    }

    [Fact]
    public void Haystack_Covers_Type_Domain_Tags_ParamNames()
    {
        var steps = new[]
        {
            S(
                "s1",
                "the step",
                type: "Given",
                domain: "Auth",
                tags: new[] { "login" },
                @params: new[] { new StepParam { Name = "username", Type = "string" } }),
            S("s2", "unrelated step", type: "Then", domain: "billing"),
        };
        var (state, _, _, _) = BuildState(steps);
        state.Open();

        state.SetQuery("login");
        Assert.Contains(state.Results, r => r.Id == "s1");

        state.SetQuery("username");
        Assert.Contains(state.Results, r => r.Id == "s1");

        state.SetQuery("Auth");
        Assert.Contains(state.Results, r => r.Id == "s1");

        state.SetQuery("Given");
        Assert.Contains(state.Results, r => r.Id == "s1");
    }
}
