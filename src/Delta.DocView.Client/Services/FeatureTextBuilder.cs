using System.Text;

namespace Delta.DocView.Client.Services;

public static class FeatureTextBuilder
{
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
            var type = items[i].Step.Type;
            var keyword = i > 0 && items[i - 1].Step.Type == type ? "And" : type;
            sb.AppendLine($"    {keyword} {items[i].Step.Pattern}");
        }

        return sb.ToString().TrimEnd();
    }
}
