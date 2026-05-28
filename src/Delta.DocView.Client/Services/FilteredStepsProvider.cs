using Delta.DocView.Shared.Models;

namespace Delta.DocView.Client.Services;

/// <summary>
/// Scoped projection of <see cref="ClientStepLibraryStore.Steps"/> through
/// <see cref="FilterEngine.Apply"/> and <see cref="StepRanking.Rank"/>. Recomputes
/// whenever the underlying FilterState or favourites change. Raises Changed
/// on every recompute so consumers can re-render or read the new list.
/// </summary>
/// <remarks>
/// Subscribers MUST unsubscribe on disposal — same contract as the underlying
/// state services. See <see cref="FilterState.Changed"/>.
/// </remarks>
public sealed class FilteredStepsProvider : IDisposable
{
    private readonly ClientStepLibraryStore _store;
    private readonly FilterState _state;
    private readonly IFavouritesStore _favs;

    public IReadOnlyList<Step> Filtered { get; private set; } = Array.Empty<Step>();

    public event Action? Changed;

    public FilteredStepsProvider(
        ClientStepLibraryStore store,
        FilterState state,
        IFavouritesStore favs)
    {
        _store = store;
        _state = state;
        _favs = favs;
        _state.Changed += OnChanged;
        _favs.Changed += OnChanged;
        Recompute();
    }

    private void OnChanged() => Recompute();

    private void Recompute()
    {
        Filtered = StepRanking.Rank(FilterEngine.Apply(_store.Steps, _state, _favs));
        Changed?.Invoke();
    }

    public void Dispose()
    {
        _state.Changed -= OnChanged;
        _favs.Changed -= OnChanged;
    }
}
