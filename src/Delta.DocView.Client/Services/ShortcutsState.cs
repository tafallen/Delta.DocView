namespace Delta.DocView.Client.Services;

/// <summary>
/// Scoped state for the keyboard shortcuts overlay: a single open/closed flag.
/// Subscribes to <see cref="IKeyboardActions.OpenShortcutsRequested"/> and
/// <see cref="IKeyboardActions.CloseOverlayRequested"/> in its constructor so
/// the global keyboard handler from US-07 opens / closes the overlay without
/// any extra wiring — same bus seam as the command palette.
/// </summary>
/// <remarks>
/// Subscribers to <see cref="Changed"/> MUST unsubscribe on disposal — same
/// contract as other state services.
/// </remarks>
public sealed class ShortcutsState : IDisposable
{
    private readonly IKeyboardActions _actions;

    public bool IsOpen { get; private set; }

    /// <summary>Raised on any observable change.</summary>
    public event Action? Changed;

    public ShortcutsState(IKeyboardActions actions)
    {
        _actions = actions;
        _actions.OpenShortcutsRequested += Open;
        _actions.CloseOverlayRequested += Close;
    }

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

    // Called when the DI root scope is disposed (app shutdown).
    // Components that subscribe to Changed must unsubscribe themselves via their own IDisposable.
    public void Dispose()
    {
        _actions.OpenShortcutsRequested -= Open;
        _actions.CloseOverlayRequested -= Close;
    }
}
