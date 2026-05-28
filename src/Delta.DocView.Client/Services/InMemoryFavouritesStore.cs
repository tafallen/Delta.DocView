namespace Delta.DocView.Client.Services;

public sealed class InMemoryFavouritesStore : IFavouritesStore
{
    private readonly HashSet<string> _ids = new();

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
