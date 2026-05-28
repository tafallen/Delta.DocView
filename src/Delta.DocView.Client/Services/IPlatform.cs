namespace Delta.DocView.Client.Services;

/// <summary>
/// Runtime platform detection. Reads navigator.platform / userAgent once at
/// boot via JSInterop; consumers display platform-appropriate keyboard
/// shortcut labels.
/// </summary>
public interface IPlatform
{
    /// <summary>True if the host appears to be macOS (Mac in platform or UA).</summary>
    bool IsMac { get; }

    /// <summary>One-shot async init, called by App.razor in Task.WhenAll on boot.</summary>
    Task InitializeAsync();

    /// <summary>Formatted chord label, e.g. "⌘K" on macOS or "Ctrl+K" elsewhere.</summary>
    string ShortcutLabel(string letter);
}
