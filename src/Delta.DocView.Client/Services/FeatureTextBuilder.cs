using System.Text;

namespace Delta.DocView.Client.Services;

public static class FeatureTextBuilder
{
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
        return sb.ToString();
    }
}
