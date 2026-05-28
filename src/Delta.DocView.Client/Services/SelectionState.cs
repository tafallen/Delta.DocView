using Delta.DocView.Shared.Models;

namespace Delta.DocView.Client.Services;

/// <summary>
/// Scoped holder of the currently-selected <see cref="Step"/>. Written by
/// <c>StepList</c> when the user picks a row; read by the detail panel
/// (US-06) which subscribes to <see cref="Changed"/>. Decouples writer and
/// reader without threading <c>EventCallback</c>s through <c>MainLayout</c>.
/// </summary>
public sealed class SelectionState
{
    /// <summary>The currently-selected step, or null when nothing is selected.</summary>
    public Step? Selected { get; private set; }

    /// <summary>Raised when <see cref="Selected"/> changes to a different reference (or to/from null).</summary>
    /// <remarks>
    /// Subscribers MUST unsubscribe on disposal (e.g. components implementing IDisposable
    /// and calling -= in Dispose). Failing to do so leaks the closure for the lifetime of
    /// this scoped service.
    /// </remarks>
    public event Action? Changed;

    /// <summary>
    /// Sets the current selection. Passing the same reference (or null when already null)
    /// is a no-op and does not raise <see cref="Changed"/>. Passing null clears the selection.
    /// </summary>
    public void Select(Step? step)
    {
        if (ReferenceEquals(Selected, step)) return;
        Selected = step;
        Changed?.Invoke();
    }
}
