using Delta.DocView.Shared.Models;

namespace Delta.DocView.Client.Services;

public sealed class KeyboardActions : IKeyboardActions
{
    private readonly SelectionState _selection;
    private readonly IFavouritesStore _favs;
    private readonly FilteredStepsProvider _provider;

    public KeyboardActions(
        SelectionState selection,
        IFavouritesStore favs,
        FilteredStepsProvider provider)
    {
        _selection = selection;
        _favs = favs;
        _provider = provider;
    }

    public void SelectNext()
    {
        var list = _provider.Filtered;
        if (list.Count == 0) return;

        var current = _selection.Selected;
        var idx = IndexOf(list, current);

        if (idx < 0)
        {
            _selection.Select(list[0]); // no selection or selection filtered out → first
            return;
        }

        if (idx >= list.Count - 1) return; // clamp at last

        _selection.Select(list[idx + 1]);
    }

    public void SelectPrev()
    {
        var list = _provider.Filtered;
        if (list.Count == 0) return;

        var current = _selection.Selected;
        if (current is null) return; // K with no selection is a no-op

        var idx = IndexOf(list, current);
        if (idx <= 0) return; // not in list or already first → no-op

        _selection.Select(list[idx - 1]);
    }

    private static int IndexOf(IReadOnlyList<Step> list, Step? target)
    {
        if (target is null) return -1;
        for (var i = 0; i < list.Count; i++)
        {
            if (ReferenceEquals(list[i], target)) return i;
        }
        return -1;
    }

    public void ToggleSelectedFavourite()
    {
        if (_selection.Selected is Step s) _favs.Toggle(s.Id);
    }

    public event Action? OpenPaletteRequested;
    public event Action? OpenShortcutsRequested;
    public event Action? ToggleComposerRequested;
    public event Action? CloseOverlayRequested;

    public void OpenPalette()    => OpenPaletteRequested?.Invoke();
    public void OpenShortcuts()  => OpenShortcutsRequested?.Invoke();
    public void ToggleComposer() => ToggleComposerRequested?.Invoke();
    public void CloseOverlay()   => CloseOverlayRequested?.Invoke();
}
