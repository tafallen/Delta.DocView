using Delta.DocView.Shared.Models;

namespace Delta.DocView.Client.Services;

/// <summary>
/// Scoped state for the command palette modal: open/closed flag, current query,
/// scored results (capped at 50), and selected index. Subscribes to
/// <see cref="IKeyboardActions.OpenPaletteRequested"/> and
/// <see cref="IKeyboardActions.CloseOverlayRequested"/> in its constructor so
/// the global keyboard handler from US-07 opens / closes the palette without
/// any extra wiring.
/// </summary>
/// <remarks>
/// Subscribers to <see cref="Changed"/> MUST unsubscribe on disposal — same
/// contract as other state services.
/// </remarks>
public sealed class PaletteState : IDisposable
{
    /// <summary>
    /// Maximum number of results shown in the palette. The palette container
    /// CSS (max-height: 70vh) is sized assuming this cap; raising the value
    /// significantly may require CSS adjustments.
    /// </summary>
    public const int MaxResults = 50;

    private readonly ClientStepLibraryStore _store;
    private readonly IKeyboardActions _actions;
    private readonly SelectionState _selection;

    public bool IsOpen { get; private set; }
    public string Query { get; private set; } = "";
    public IReadOnlyList<Step> Results { get; private set; } = Array.Empty<Step>();
    public int SelectedIndex { get; private set; }

    public Step? CurrentResult =>
        SelectedIndex >= 0 && SelectedIndex < Results.Count ? Results[SelectedIndex] : null;

    /// <summary>Raised on any observable change.</summary>
    public event Action? Changed;

    public PaletteState(
        ClientStepLibraryStore store,
        IKeyboardActions actions,
        SelectionState selection)
    {
        _store = store;
        _actions = actions;
        _selection = selection;
        _actions.OpenPaletteRequested += Open;
        _actions.CloseOverlayRequested += Close;
    }

    /// <summary>
    /// Opens the palette and seeds the result list with the top steps by usage.
    /// </summary>
    /// <remarks>
    /// Designed to be called after the library has finished loading
    /// (App.razor gates MainLayout rendering on Task.WhenAll over
    /// LibraryClient.LoadAsync and Favourites.InitializeAsync, both of which
    /// must complete before any keyboard or click can reach this method).
    /// Calling Open() with an empty store produces an empty result list and
    /// does not throw, but is not the expected runtime path.
    /// </remarks>
    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        Query = "";
        Recompute();
        Changed?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Query = "";
        Results = Array.Empty<Step>();
        SelectedIndex = 0;
        Changed?.Invoke();
    }

    public void SetQuery(string query)
    {
        var q = query ?? "";
        if (q == Query) return;
        Query = q;
        Recompute();
        Changed?.Invoke();
    }

    public void MoveSelectionDown()
    {
        if (Results.Count == 0) return;
        var next = Math.Min(SelectedIndex + 1, Results.Count - 1);
        if (next == SelectedIndex) return;
        SelectedIndex = next;
        Changed?.Invoke();
    }

    public void MoveSelectionUp()
    {
        if (Results.Count == 0) return;
        var next = Math.Max(SelectedIndex - 1, 0);
        if (next == SelectedIndex) return;
        SelectedIndex = next;
        Changed?.Invoke();
    }

    public void SetSelectedIndex(int idx)
    {
        if (idx < 0 || idx >= Results.Count) return;
        if (idx == SelectedIndex) return;
        SelectedIndex = idx;
        Changed?.Invoke();
    }

    public void SelectCurrent()
    {
        if (CurrentResult is Step s)
        {
            _selection.Select(s);
            Close();
        }
    }

    private void Recompute()
    {
        SelectedIndex = 0;
        if (string.IsNullOrWhiteSpace(Query))
        {
            Results = _store.Steps
                .OrderByDescending(s => s.Used)
                .Take(MaxResults)
                .ToList();
            return;
        }

        Results = _store.Steps
            .Select(s => (Step: s, Score: FuzzySearch.Score(Query, _store.Haystacks.TryGetValue(s.Id, out var hay) ? hay : "")))
            .Where(t => t.Score > 0)
            .OrderByDescending(t => t.Score)
            .Take(MaxResults)
            .Select(t => t.Step)
            .ToList();
    }

    // Called when the DI root scope is disposed (app shutdown).
    // Components that subscribe to Changed must unsubscribe themselves via their own IDisposable.
    public void Dispose()
    {
        _actions.OpenPaletteRequested -= Open;
        _actions.CloseOverlayRequested -= Close;
    }
}
