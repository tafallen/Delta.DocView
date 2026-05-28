namespace Delta.DocView.Client.Services;

/// <summary>
/// Tracks the user's favourited step ids. v1 is in-memory; US-05 swaps in a localStorage-backed
/// implementation.
/// </summary>
public interface IFavouritesStore
{
    /// <summary>Returns true if the given step id is currently favourited.</summary>
    bool Has(string id);

    /// <summary>Toggles favourite state for the given step id.</summary>
    void Toggle(string id);

    /// <summary>Number of favourited step ids.</summary>
    int Count { get; }

    /// <summary>All favourited step ids.</summary>
    IReadOnlyCollection<string> All { get; }

    /// <summary>Raised when the set of favourites changes.</summary>
    event Action? Changed;
}
