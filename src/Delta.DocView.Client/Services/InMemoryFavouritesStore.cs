namespace Delta.DocView.Client.Services;

/// <summary>
/// In-memory IFavouritesStore implementation.
/// </summary>
/// <remarks>
/// Used by tests for fast, deterministic favourites without JSInterop.
/// Production registration as of US-05 is <see cref="LocalStorageFavouritesStore"/>.
/// </remarks>
public sealed class InMemoryFavouritesStore : FavouritesStoreBase
{
    public override Task InitializeAsync() => Task.CompletedTask;
}
