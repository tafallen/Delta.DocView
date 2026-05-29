namespace Delta.DocView.Client.Services;

/// <summary>
/// Scoped open/closed state for the US-11 appearance tweaks panel: a single flag.
/// Unlike <see cref="ShortcutsState"/> there is no keyboard shortcut for the tweaks
/// panel, so this service has no <see cref="IKeyboardActions"/> subscription and needs
/// no disposal — it is opened only via the header gear button.
/// </summary>
/// <remarks>
/// Subscribers to <see cref="Changed"/> MUST unsubscribe on disposal, since this store
/// is scoped and outlives transient components.
/// </remarks>
public sealed class TweaksPanelState
{
    public bool IsOpen { get; private set; }

    /// <summary>Raised on any observable change.</summary>
    public event Action? Changed;

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        Changed?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Changed?.Invoke();
    }

    public void Toggle()
    {
        IsOpen = !IsOpen;
        Changed?.Invoke();
    }
}
