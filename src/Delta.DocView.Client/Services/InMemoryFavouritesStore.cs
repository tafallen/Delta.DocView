namespace Delta.DocView.Client.Services;

/// <remarks>
/// Used by tests for fast, deterministic favourites without JSInterop.
/// Production registration as of US-05 is <see cref="LocalStorageFavouritesStore"/>.
/// </remarks>
public sealed class InMemoryFavouritesStore : IFavouritesStore
{
    private readonly HashSet<string> _ids = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public bool Has(string id) => _ids.Contains(id);

    public void Toggle(string id)
    {
        if (!_ids.Remove(id))
        {
            _ids.Add(id);
        }
        Changed?.Invoke();
    }

    public int Count => _ids.Count;

    public IReadOnlyCollection<string> All => _ids.ToArray();

    public event Action? Changed;
}
