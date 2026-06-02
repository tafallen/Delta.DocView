using System.Text;

namespace Delta.DocView.Client.Services;

public static class FeatureTextBuilder
{
    /// <summary>Indent prepended to each DataTable row relative to the file root.
    /// 6 spaces = 4 (step) + 2 (table under step) in a standard SpecFlow .feature.</summary>
    private const string TableIndent = "      ";
    public static string GetKeyword(int index, IReadOnlyList<ComposerItem> items)
    {
        var type = items[index].Step.Type;
        return index > 0 && items[index - 1].Step.Type == type ? "And" : type;
    }

    public static string Build(string scenarioName, IReadOnlyList<ComposerItem> items)
    {
        if (items.Count == 0) return "";

        var name = string.IsNullOrWhiteSpace(scenarioName) ? "Untitled scenario" : scenarioName;
        var sb = new StringBuilder();
        sb.AppendLine($"Feature: {name}");
        sb.AppendLine();
        sb.AppendLine($"  Scenario: {name}");

        for (int i = 0; i < items.Count; i++)
        {
            sb.AppendLine($"    {GetKeyword(i, items)} {RenderStep(items[i])}");
        }

        return sb.ToString().TrimEnd();
    }

    private static string RenderStep(ComposerItem item)
    {
        var tokens = PatternTokeniser.Tokenise(item.Step.Pattern);
        var sb = new System.Text.StringBuilder();
        int paramIdx = 0;
        foreach (var token in tokens)
        {
            if (token is StaticText s)
                sb.Append(s.Text);
            else if (token is ParamToken)
            {
                sb.Append(paramIdx < item.ParamValues.Count ? item.ParamValues[paramIdx] : "");
                paramIdx++;
            }
        }

        // Append DataTable column header + empty data row for table-typed params
        foreach (var p in item.Step.Params.Where(param => param.HasTable && param.Columns.Count > 0))
        {
            sb.Append(AppendTableRows(p.Columns.Select(c => c.Name)));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds a Gherkin DataTable block from the given column names:
    ///   | Col1 | Col2 |
    ///   |      |      |
    /// Returns the string with a leading newline so it can be appended directly.
    /// </summary>
    public static string AppendTableRows(IEnumerable<string> columns, string indent = TableIndent)
    {
        var cols = columns.ToList();
        if (cols.Count == 0) return "";
        var header = $"\n{indent}| {string.Join(" | ", cols)} |";
        var emptyRow = $"\n{indent}| {string.Join(" | ", cols.Select(_ => "  "))} |";
        return header + emptyRow;
    }
}
