namespace Delta.DocView.Client.Services;

/// <summary>
/// Ordinal case-insensitive subsequence scorer for the command palette.
/// Returns 0 when <paramref name="needle"/> is empty, doesn't fully match,
/// or any input is null. Higher scores indicate better matches.
/// </summary>
/// <remarks>
/// Scoring per matched character:
/// <list type="bullet">
///   <item>base 10</item>
///   <item>+15 if matched at index 0 of the haystack</item>
///   <item>+8 if preceded by a word-boundary character (space, '.', '_', '-', '/')</item>
///   <item>+5 if consecutive with the previous match</item>
/// </list>
/// </remarks>
public static class FuzzySearch
{
    private static readonly char[] BoundaryChars = { ' ', '.', '_', '-', '/' };

    public static int Score(string needle, string hay)
    {
        if (string.IsNullOrEmpty(needle) || string.IsNullOrEmpty(hay)) return 0;

        var n = needle;
        var h = hay;
        var nIdx = 0;
        var score = 0;
        var prevMatchIdx = -2;

        for (var hIdx = 0; hIdx < h.Length && nIdx < n.Length; hIdx++)
        {
            if (char.ToLowerInvariant(h[hIdx]) != char.ToLowerInvariant(n[nIdx])) continue;

            var bonus = 10;
            if (hIdx == 0) bonus += 15;
            else if (Array.IndexOf(BoundaryChars, h[hIdx - 1]) >= 0) bonus += 8;
            if (hIdx == prevMatchIdx + 1) bonus += 5;

            score += bonus;
            prevMatchIdx = hIdx;
            nIdx++;
        }

        return nIdx == n.Length ? score : 0;
    }
}
