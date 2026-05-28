namespace Delta.DocView.Client.Services;

/// <summary>
/// Single seam through which every keyboard action funnels. Direct-method
/// actions (SelectNext, SelectPrev, ToggleSelectedFavourite) own behaviour
/// locally; event-fired actions (OpenPalette, OpenShortcuts, ToggleComposer,
/// CloseOverlay) stay open for US-08/09/10 state objects to subscribe to
/// without modifying this dispatch layer.
/// </summary>
public interface IKeyboardActions
{
    void SelectNext();
    void SelectPrev();
    void ToggleSelectedFavourite();
    void OpenPalette();
    void OpenShortcuts();
    void ToggleComposer();
    void CloseOverlay();

    event Action? OpenPaletteRequested;
    event Action? OpenShortcutsRequested;
    event Action? ToggleComposerRequested;
    event Action? CloseOverlayRequested;
}
