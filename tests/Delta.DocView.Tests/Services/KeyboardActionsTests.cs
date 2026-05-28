using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;

namespace Delta.DocView.Tests.Services;

public class KeyboardActionsTests
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

    private sealed class Harness : IDisposable
    {
        public ClientStepLibraryStore Store { get; }
        public FilterState State { get; }
        public InMemoryFavouritesStore Favs { get; }
        public FilteredStepsProvider Provider { get; }
        public SelectionState Selection { get; }
        public KeyboardActions Actions { get; }

        public Harness()
        {
            Store = new ClientStepLibraryStore();
            Store.Populate(new StepLibrary
            {
                Steps =
                [
                    S("step-1", "a user logs in",       used: 50),
                    S("step-2", "b user clicks login",  used: 40),
                    S("step-3", "c user sees dashboard", used: 30),
                    S("step-4", "d user logs out",       used: 20),
                    S("step-5", "e user sees error",     used: 10),
                ],
                Domains = [D("auth", "Auth")],
            });
            State = new FilterState();
            Favs = new InMemoryFavouritesStore();
            Provider = new FilteredStepsProvider(Store, State, Favs);
            Selection = new SelectionState();
            Actions = new KeyboardActions(Selection, Favs, Provider);
        }

        public void Dispose() => Provider.Dispose();
    }

    [Fact]
    public void SelectNext_WithNoSelection_SelectsFirstFiltered()
    {
        using var h = new Harness();

        h.Actions.SelectNext();

        Assert.Same(h.Provider.Filtered[0], h.Selection.Selected);
    }

    [Fact]
    public void SelectNext_FromMiddle_MovesForward()
    {
        using var h = new Harness();
        h.Selection.Select(h.Provider.Filtered[2]);

        h.Actions.SelectNext();

        Assert.Same(h.Provider.Filtered[3], h.Selection.Selected);
    }

    [Fact]
    public void SelectNext_AtLastItem_NoOp()
    {
        using var h = new Harness();
        var last = h.Provider.Filtered[^1];
        h.Selection.Select(last);

        h.Actions.SelectNext();

        Assert.Same(last, h.Selection.Selected);
    }

    [Fact]
    public void SelectNext_FromSelectionNotInFilteredList_SelectsFirstFiltered()
    {
        using var h = new Harness();
        var picked = h.Provider.Filtered[2];
        h.Selection.Select(picked);

        // Toggle FavsOnly with no favourites → empty list, SelectNext is no-op.
        h.State.SetFavsOnly(true);
        Assert.Empty(h.Provider.Filtered);
        h.Actions.SelectNext();
        Assert.Same(picked, h.Selection.Selected);

        // Add a different step as favourite while FavsOnly remains on.
        var other = h.Store.Steps.First(s => s.Id != picked.Id);
        h.Favs.Toggle(other.Id);
        Assert.Single(h.Provider.Filtered);
        Assert.DoesNotContain(h.Provider.Filtered, s => ReferenceEquals(s, picked));

        h.Actions.SelectNext();

        Assert.Same(h.Provider.Filtered[0], h.Selection.Selected);
    }

    [Fact]
    public void SelectPrev_WithNoSelection_NoOp()
    {
        using var h = new Harness();

        h.Actions.SelectPrev();

        Assert.Null(h.Selection.Selected);
    }

    [Fact]
    public void SelectPrev_AtFirstItem_NoOp()
    {
        using var h = new Harness();
        var first = h.Provider.Filtered[0];
        h.Selection.Select(first);

        h.Actions.SelectPrev();

        Assert.Same(first, h.Selection.Selected);
    }

    [Fact]
    public void SelectPrev_FromMiddle_MovesBackward()
    {
        using var h = new Harness();
        h.Selection.Select(h.Provider.Filtered[3]);

        h.Actions.SelectPrev();

        Assert.Same(h.Provider.Filtered[2], h.Selection.Selected);
    }

    [Fact]
    public void ToggleSelectedFavourite_WithSelection_TogglesById()
    {
        using var h = new Harness();
        var step = h.Provider.Filtered[1];
        h.Selection.Select(step);

        h.Actions.ToggleSelectedFavourite();
        Assert.True(h.Favs.Has(step.Id));

        h.Actions.ToggleSelectedFavourite();
        Assert.False(h.Favs.Has(step.Id));
    }

    [Fact]
    public void ToggleSelectedFavourite_NoSelection_NoOp()
    {
        using var h = new Harness();

        var ex = Record.Exception(() => h.Actions.ToggleSelectedFavourite());

        Assert.Null(ex);
        Assert.Equal(0, h.Favs.Count);
    }

    [Fact]
    public void OpenPalette_RaisesOpenPaletteRequested()
    {
        using var h = new Harness();
        var count = 0;
        h.Actions.OpenPaletteRequested += () => count++;

        h.Actions.OpenPalette();

        Assert.Equal(1, count);
    }

    [Fact]
    public void OpenShortcuts_RaisesOpenShortcutsRequested()
    {
        using var h = new Harness();
        var count = 0;
        h.Actions.OpenShortcutsRequested += () => count++;

        h.Actions.OpenShortcuts();

        Assert.Equal(1, count);
    }

    [Fact]
    public void ToggleComposer_RaisesToggleComposerRequested()
    {
        using var h = new Harness();
        var count = 0;
        h.Actions.ToggleComposerRequested += () => count++;

        h.Actions.ToggleComposer();

        Assert.Equal(1, count);
    }

    [Fact]
    public void CloseOverlay_RaisesCloseOverlayRequested()
    {
        using var h = new Harness();
        var count = 0;
        h.Actions.CloseOverlayRequested += () => count++;

        h.Actions.CloseOverlay();

        Assert.Equal(1, count);
    }
}
