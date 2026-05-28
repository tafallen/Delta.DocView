using System.Text.RegularExpressions;

namespace Delta.DocView.Client.Services;

public readonly record struct HighlightedToken(string Text, string CssClass);

/// <summary>
/// Minimal C# syntax highlighter. Recognises a fixed keyword set plus
/// strings, comments, and integer literals. NOT a full lexer — falls
/// back to plain `cs-text` for everything else. Suitable for v1 read-only
/// display in the detail panel.
/// </summary>
public static class CSharpHighlighter
{
    private static readonly HashSet<string> Keywords = new()
    {
        "public","private","protected","internal","static","readonly","sealed",
        "abstract","virtual","override","new","var","void","class","record","struct",
        "interface","enum","using","namespace","if","else","for","foreach","while",
        "return","break","continue","switch","case","default","string","int","bool",
        "double","decimal","Task","async","await","null","true","false","try","catch",
        "finally","throw","this","base","ref","out","in"
    };

    // Order matters: comments first, then strings, then numbers/identifiers.
    private static readonly Regex TokenRegex = new(
        @"(?<comment>//[^\n]*|/\*[\s\S]*?\*/)|" +
        @"(?<string>@""([^""]|"""")*""|\$""([^""\\]|\\.)*""|""([^""\\]|\\.)*"")|" +
        @"(?<number>\b\d+\b)|" +
        @"(?<ident>\b[A-Za-z_][A-Za-z0-9_]*\b)",
        RegexOptions.Compiled);

    public static IReadOnlyList<HighlightedToken> Tokenise(string source)
    {
        if (string.IsNullOrEmpty(source)) return Array.Empty<HighlightedToken>();

        var result = new List<HighlightedToken>();
        var lastEnd = 0;

        foreach (Match m in TokenRegex.Matches(source))
        {
            if (m.Index > lastEnd)
                result.Add(new HighlightedToken(source[lastEnd..m.Index], "cs-text"));

            string cssClass;
            if (m.Groups["comment"].Success)      cssClass = "cs-comment";
            else if (m.Groups["string"].Success)  cssClass = "cs-string";
            else if (m.Groups["number"].Success)  cssClass = "cs-number";
            else // ident
                cssClass = Keywords.Contains(m.Value) ? "cs-kw" : "cs-text";

            result.Add(new HighlightedToken(m.Value, cssClass));
            lastEnd = m.Index + m.Length;
        }

        if (lastEnd < source.Length)
            result.Add(new HighlightedToken(source[lastEnd..], "cs-text"));

        return result;
    }
}
