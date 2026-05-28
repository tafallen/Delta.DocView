namespace Delta.DocView.Client.Services;

/// <summary>
/// Tracks the user's favourited step ids. v1 is in-memory; US-05 swaps in a localStorage-backed
/// implementation. <see cref="InitializeAsync"/> is the first call — it must complete before any
/// sync mutation (<see cref="Toggle"/>, <see cref="Has"/>, <see cref="All"/>, <see cref="Count"/>)
/// is invoked.
/// </summary>
public interface IFavouritesStore
{
    /// <summary>
    /// One-shot hydration on app startup. Sync mutations may be called once this completes.
    /// v1 (in-memory) returns immediately; v2 (localStorage) reads stored ids via JSInterop.
    /// </summary>
    Task InitializeAsync();

    /// <summary>Returns true if the given step id is currently favourited.</summary>
    bool Has(string id);

    /// <summary>Toggles favourite state for the given step id.</summary>
    void Toggle(string id);

    /// <summary>Number of favourited step ids.</summary>
    int Count { get; }

    /// <summary>All favourited step ids.</summary>
    IReadOnlyCollection<string> All { get; }

    /// <summary>Raised when the set of favourites changes.</summary>
    /// <remarks>
    /// Subscribers MUST unsubscribe on disposal (e.g. components implementing IDisposable
    /// and calling -= in Dispose). Failing to do so leaks the closure for the lifetime of
    /// this scoped service.
    /// </remarks>
    event Action? Changed;
}
