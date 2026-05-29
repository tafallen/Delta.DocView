using Delta.DocView.Client.Services;

namespace Delta.DocView.Tests.Services;

public class ShortcutsStateTests
{
    private static (ShortcutsState state, FakeKeyboardActions actions) Create()
    {
        var actions = new FakeKeyboardActions();
        return (new ShortcutsState(actions), actions);
    }

    [Fact]
    public void Open_From_OpenShortcutsRequested_Opens()
    {
        var (state, actions) = Create();
        var changed = 0;
        state.Changed += () => changed++;

        actions.OpenShortcuts();

        Assert.True(state.IsOpen);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Close_From_CloseOverlayRequested_Closes()
    {
        var (state, actions) = Create();
        actions.OpenShortcuts();

        actions.CloseOverlay();

        Assert.False(state.IsOpen);
    }

    [Fact]
    public void Open_When_Already_Open_NoOp()
    {
        var (state, actions) = Create();
        actions.OpenShortcuts();
        var changed = 0;
        state.Changed += () => changed++;

        actions.OpenShortcuts();

        Assert.True(state.IsOpen);
        Assert.Equal(0, changed);
    }

    [Fact]
    public void Close_When_Closed_NoOp()
    {
        var (state, _) = Create();
        var changed = 0;
        state.Changed += () => changed++;

        state.Close();

        Assert.False(state.IsOpen);
        Assert.Equal(0, changed);
    }

    [Fact]
    public void Toggle_Flips_State()
    {
        var (state, _) = Create();

        state.Toggle();
        Assert.True(state.IsOpen);

        state.Toggle();
        Assert.False(state.IsOpen);
    }

    [Fact]
    public void Disposal_Unsubscribes_From_Actions()
    {
        var (state, actions) = Create();
        state.Dispose();

        actions.OpenShortcuts();

        Assert.False(state.IsOpen);
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
