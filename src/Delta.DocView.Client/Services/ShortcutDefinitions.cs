namespace Delta.DocView.Client.Services;

/// <summary>
/// The shortcut list shown in the overlay. This is the DISPLAY source of
/// truth; the actual key handling lives in docview.keyboard (wwwroot/js/docview.js).
/// Keep the two in sync — if you change a binding here, change it there too.
/// </summary>
public static class ShortcutDefinitions
{
    public static IReadOnlyList<Shortcut> For(IPlatform platform) =>
    [
        new Shortcut("Open command palette", [platform.ShortcutLabel("K"), "/"]),
        new Shortcut("Show keyboard shortcuts", ["?"]),
        new Shortcut("Toggle scenario composer", ["C"]),
        new Shortcut("Toggle favourite on selected step", ["F"]),
        new Shortcut("Move selection down", ["J"]),
        new Shortcut("Move selection up", ["K"]),
        new Shortcut("Close overlay", ["Esc"]),
    ];
}
