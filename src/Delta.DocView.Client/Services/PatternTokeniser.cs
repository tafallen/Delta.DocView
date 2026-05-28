using System.Text.RegularExpressions;

namespace Delta.DocView.Client.Services;

/// <summary>
/// Parses SpecFlow-style step patterns into a sequence of static text and
/// parameter tokens. Recognises {type} and {name : type} forms; malformed
/// shapes ({}, {name:}) fall back to literal text.
/// </summary>
public static class PatternTokeniser
{
    private static readonly Regex TokenRegex = new(@"\{([^}]*)\}", RegexOptions.Compiled);

    /// <summary>Returns the tokens for <paramref name="pattern"/> in document order.</summary>
    public static IReadOnlyList<PatternToken> Tokenise(string pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return Array.Empty<PatternToken>();

        var result = new List<PatternToken>();
        var lastEnd = 0;

        foreach (Match m in TokenRegex.Matches(pattern))
        {
            if (m.Index > lastEnd)
                result.Add(new StaticText(pattern[lastEnd..m.Index]));

            var inner = m.Groups[1].Value;
            var token = ClassifyToken(inner, m.Value);
            result.Add(token);

            lastEnd = m.Index + m.Length;
        }

        if (lastEnd < pattern.Length)
            result.Add(new StaticText(pattern[lastEnd..]));

        return result;
    }

    private static PatternToken ClassifyToken(string inner, string raw)
    {
        if (string.IsNullOrWhiteSpace(inner))
            return new StaticText(raw); // "{}" → literal

        var colon = inner.IndexOf(':');
        if (colon < 0)
        {
            var type = inner.Trim();
            return type.Length == 0 ? new StaticText(raw) : new ParamToken(null, type);
        }

        var nm = inner[..colon].Trim();
        var ty = inner[(colon + 1)..].Trim();

        if (ty.Length == 0)
            return new StaticText(raw); // "{name:}" → literal

        return new ParamToken(nm.Length == 0 ? null : nm, ty);
    }
}
