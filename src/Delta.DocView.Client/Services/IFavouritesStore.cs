namespace Delta.DocView.Client.Services;

public interface IFavouritesStore
{
    bool Has(string id);
    void Toggle(string id);
    int Count { get; }
    IReadOnlyCollection<string> All { get; }
    event Action? Changed;
}
