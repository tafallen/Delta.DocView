using Delta.DocView.Shared.Models;

namespace Delta.DocView.Client.Services;

/// <summary>
/// Canonical step ordering used wherever the library or a filtered subset is rendered.
/// Sorted by <see cref="Step.Used"/> descending, with <see cref="Step.Pattern"/> ascending
/// (ordinal) as a stable tie-break so equal-usage rows stay in a deterministic order.
/// </summary>
public static class StepRanking
{
    /// <summary>
    /// Returns the input ordered by usage descending then pattern ordinal ascending.
    /// </summary>
    public static IReadOnlyList<Step> Rank(IEnumerable<Step> steps) =>
        steps
            .OrderByDescending(s => s.Used)
            .ThenBy(s => s.Pattern, StringComparer.Ordinal)
            .ToList();
}
