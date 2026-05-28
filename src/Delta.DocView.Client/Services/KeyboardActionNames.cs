namespace Delta.DocView.Client.Services;

/// <summary>
/// Action name strings used to cross the JS → .NET boundary in
/// docview.keyboard.attach's invokeMethodAsync('OnKey', name).
/// </summary>
public static class KeyboardActionNames
{
    public const string SelectNext     = "select-next";
    public const string SelectPrev     = "select-prev";
    public const string ToggleFav      = "toggle-fav";
    public const string OpenPalette    = "open-palette";
    public const string OpenShortcuts  = "open-shortcuts";
    public const string ToggleComposer = "toggle-composer";
    public const string CloseOverlay   = "close-overlay";
}
