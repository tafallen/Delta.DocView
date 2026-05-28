namespace Delta.DocView.Client.Services;

/// <summary>
/// Shared in-memory bookkeeping for IFavouritesStore implementations.
/// Subclasses override PersistAsync (and InitializeAsync) to add persistence;
/// the set semantics, change-notification, and Toggle behaviour stay here.
/// </summary>
public abstract class FavouritesStoreBase : IFavouritesStore
{
    private readonly HashSet<string> _ids = new();

    public int Count => _ids.Count;
    public bool Has(string id) => _ids.Contains(id);
    public IReadOnlyCollection<string> All => _ids.ToArray();
    public event Action? Changed;

    public void Toggle(string id)
    {
        if (!_ids.Add(id)) _ids.Remove(id);
        Changed?.Invoke();
        _ = PersistAsync();
    }

    /// <summary>
    /// Replaces the in-memory set with the given ids. Used by InitializeAsync
    /// implementations after hydration completes. Does NOT raise Changed —
    /// init runs before any subscriber attaches.
    /// </summary>
    protected void Hydrate(IEnumerable<string> ids)
    {
        _ids.Clear();
        foreach (var id in ids) _ids.Add(id);
    }

    /// <summary>Snapshot of the current set in ordinal-ascending order.</summary>
    protected IReadOnlyList<string> Snapshot() =>
        _ids.OrderBy(s => s, StringComparer.Ordinal).ToList();

    /// <summary>Override to add persistence. Default is a no-op.</summary>
    protected virtual Task PersistAsync() => Task.CompletedTask;

    public abstract Task InitializeAsync();
}
