using System.Text;

namespace Delta.DocView.Client.Services;

/// <summary>
/// Deterministic mapping from domain id to a curated, colour-blind-safe palette slot.
/// Uses FNV-1a 32-bit hash modulo the palette size so the mapping is stable across
/// runs (unlike <see cref="string.GetHashCode()"/>).
/// </summary>
public static class DomainPalette
{
    private const uint FnvOffsetBasis = 2166136261;
    private const uint FnvPrime = 16777619;

    /// <summary>
    /// Curated 12-colour palette selected for deuteranopia/protanopia friendliness
    /// (Okabe-Ito-style). Indices are stable; do not reorder without a migration.
    /// </summary>
    private static readonly string[] Palette =
    [
        "hsl(0, 0%, 30%)",      // dark grey
        "hsl(30, 80%, 50%)",    // orange
        "hsl(200, 80%, 50%)",   // sky blue
        "hsl(140, 50%, 45%)",   // bluish green
        "hsl(55, 90%, 55%)",    // yellow
        "hsl(220, 60%, 50%)",   // strong blue
        "hsl(15, 75%, 55%)",    // vermillion
        "hsl(320, 50%, 55%)",   // reddish purple
        "hsl(260, 50%, 55%)",   // violet
        "hsl(170, 60%, 45%)",   // teal
        "hsl(90, 50%, 45%)",    // olive-green
        "hsl(0, 70%, 50%)",     // red
    ];

    public static int PaletteSize => Palette.Length;

    /// <summary>
    /// Returns the palette slot in [0, 12) for the given id. Empty/null id returns 0.
    /// </summary>
    public static int IndexFor(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return 0;
        }

        var bytes = Encoding.UTF8.GetBytes(id);
        uint hash = FnvOffsetBasis;
        for (int i = 0; i < bytes.Length; i++)
        {
            hash ^= bytes[i];
            hash *= FnvPrime;
        }

        return (int)(hash % (uint)Palette.Length);
    }

    /// <summary>
    /// Returns the HSL CSS string from the curated palette for the given id.
    /// </summary>
    public static string CssVarValue(string id) => Palette[IndexFor(id)];

    /// <summary>
    /// Returns a CSS-safe custom-property name of the form <c>--dom-{safe}</c> where
    /// <c>safe</c> is the lower-cased id with any character outside <c>[a-z0-9-]</c>
    /// replaced by <c>-</c>. Empty/null id returns <c>--dom-default</c>.
    /// </summary>
    public static string CssVarName(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return "--dom-default";
        }

        var lower = id.ToLowerInvariant();
        var sb = new StringBuilder(lower.Length);
        for (int i = 0; i < lower.Length; i++)
        {
            var c = lower[i];
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-')
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('-');
            }
        }

        return $"--dom-{sb}";
    }
}
