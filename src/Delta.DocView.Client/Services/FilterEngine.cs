using Delta.DocView.Shared.Models;

namespace Delta.DocView.Client.Services;

/// <summary>
/// Pure filter projection over the step library, driven by <see cref="FilterState"/> and
/// <see cref="IFavouritesStore"/>.
/// </summary>
public static class FilterEngine
{
    /// <summary>
    /// Applies all active filters in narrowing order: type → domain → param types → favourites-only
    /// → query substring (case-insensitive ordinal).
    /// </summary>
    public static IReadOnlyList<Step> Apply(
        IEnumerable<Step> steps,
        FilterState state,
        IFavouritesStore favs)
    {
        IEnumerable<Step> query = steps.Where(s => state.Types.Contains(s.Type));

        if (state.Domain is not null)
        {
            query = query.Where(s => s.Domain == state.Domain);
        }

        if (state.ParamTypes.Count > 0)
        {
            query = query.Where(s => s.Params.Any(p => state.ParamTypes.Contains(p.Type)));
        }

        if (state.FavsOnly)
        {
            query = query.Where(s => favs.Has(s.Id));
        }

        if (!string.IsNullOrEmpty(state.Query))
        {
            query = query.Where(s =>
                s.Pattern.Contains(state.Query, StringComparison.OrdinalIgnoreCase));
        }

        return query.ToList();
    }
}
