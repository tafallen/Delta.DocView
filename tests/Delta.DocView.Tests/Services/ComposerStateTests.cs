using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;

namespace Delta.DocView.Tests.Services;

public class ComposerStateTests
{
    private static Step MakeStep(string id = "s1", string type = "Given") => new()
    {
        Id = id, Type = type, Pattern = $"pattern {id}", Domain = "Auth",
        File = "T.cs", Line = 1
    };

    private static (ComposerState state, FakeKeyboardActions actions) Create()
    {
        var actions = new FakeKeyboardActions();
        return (new ComposerState(actions), actions);
    }

    [Fact]
    public void InitialState_IsClosedAndEmpty()
    {
        var (state, _) = Create();
        Assert.False(state.IsOpen);
        Assert.Empty(state.Steps);
        Assert.Equal("", state.ScenarioName);
    }

    [Fact]
    public void AddStep_AddsToList_AndOpensComposer()
    {
        var (state, _) = Create();
        state.AddStep(MakeStep());
        Assert.Single(state.Steps);
        Assert.True(state.IsOpen);
    }

    [Fact]
    public void AddStep_RaisesChanged()
    {
        var (state, _) = Create();
        var changed = false;
        state.Changed += () => changed = true;
        state.AddStep(MakeStep());
        Assert.True(changed);
    }

    [Fact]
    public void RemoveStep_RemovesById()
    {
        var (state, _) = Create();
        state.AddStep(MakeStep("s1"));
        var id = state.Steps[0].Id;
        state.RemoveStep(id);
        Assert.Empty(state.Steps);
    }

    [Fact]
    public void RemoveStep_UnknownId_IsNoOp()
    {
        var (state, _) = Create();
        state.AddStep(MakeStep("s1"));
        state.RemoveStep(Guid.NewGuid());
        Assert.Single(state.Steps);
    }

    [Fact]
    public void Toggle_FlipsIsOpen()
    {
        var (state, _) = Create();
        state.Toggle();
        Assert.True(state.IsOpen);
        state.Toggle();
        Assert.False(state.IsOpen);
    }

    [Fact]
    public void ToggleComposerRequested_TogglesState()
    {
        var (state, actions) = Create();
        actions.ToggleComposer();
        Assert.True(state.IsOpen);
    }

    [Fact]
    public void CloseOverlayRequested_ClosesWhenOpen()
    {
        var (state, actions) = Create();
        state.Toggle();
        actions.CloseOverlay();
        Assert.False(state.IsOpen);
    }

    [Fact]
    public void SetScenarioName_UpdatesName_RaisesChanged()
    {
        var (state, _) = Create();
        var changed = false;
        state.Changed += () => changed = true;
        state.SetScenarioName("My Test");
        Assert.Equal("My Test", state.ScenarioName);
        Assert.True(changed);
    }

    [Fact]
    public void Clear_EmptiesStepsAndName()
    {
        var (state, _) = Create();
        state.AddStep(MakeStep());
        state.SetScenarioName("Test");
        state.Clear();
        Assert.Empty(state.Steps);
        Assert.Equal("", state.ScenarioName);
    }

    [Fact]
    public void MoveStep_ReordersCorrectly()
    {
        var (state, _) = Create();
        state.AddStep(MakeStep("s1"));
        state.AddStep(MakeStep("s2"));
        state.AddStep(MakeStep("s3"));
        var id = state.Steps[0].Id;
        state.MoveStep(id, 2);
        Assert.Equal("s2", state.Steps[0].Step.Id);
        Assert.Equal("s3", state.Steps[1].Step.Id);
        Assert.Equal("s1", state.Steps[2].Step.Id);
    }

    [Fact]
    public void FeatureText_ReflectsCurrentSteps()
    {
        var (state, _) = Create();
        state.AddStep(MakeStep("s1", "Given"));
        state.SetScenarioName("Smoke");
        Assert.Contains("Given pattern s1", state.FeatureText);
        Assert.Contains("Smoke", state.FeatureText);
    }

    [Fact]
    public void Dispose_UnsubscribesFromKeyboardEvents()
    {
        var (state, actions) = Create();
        state.Dispose();
        actions.ToggleComposer();
        Assert.False(state.IsOpen);
    }

    [Fact]
    public void FeatureText_ReturnsSameReferenceWhenUnchanged()
    {
        var (state, _) = Create();
        state.AddStep(MakeStep("s1", "Given"));
        var first  = state.FeatureText;
        var second = state.FeatureText;
        Assert.Same(first, second); // cached — same string instance
    }

    [Fact]
    public void FeatureText_RecomputesAfterMutation()
    {
        var (state, _) = Create();
        state.AddStep(MakeStep("s1", "Given"));
        var before = state.FeatureText;
        state.AddStep(MakeStep("s2", "When"));
        var after = state.FeatureText;
        Assert.NotEqual(before, after);
    }

    private sealed class FakeKeyboardActions : IKeyboardActions
    {
        public event Action? OpenPaletteRequested;
        public event Action? OpenShortcutsRequested;
        public event Action? ToggleComposerRequested;
        public event Action? CloseOverlayRequested;

        public void SelectNext() { }
        public void SelectPrev() { }
        public void ToggleSelectedFavourite() { }
        public void OpenPalette()    => OpenPaletteRequested?.Invoke();
        public void OpenShortcuts()  => OpenShortcutsRequested?.Invoke();
        public void ToggleComposer() => ToggleComposerRequested?.Invoke();
        public void CloseOverlay()   => CloseOverlayRequested?.Invoke();
    }
}
