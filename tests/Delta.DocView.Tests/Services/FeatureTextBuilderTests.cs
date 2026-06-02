using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;

namespace Delta.DocView.Tests.Services;

public class FeatureTextBuilderTests
{
    private static Step MakeStep(string type, string pattern) => new()
    {
        Id = Guid.NewGuid().ToString(), Type = type, Pattern = pattern,
        Domain = "Auth", File = "T.cs", Line = 1
    };

    private static ComposerItem Item(string type, string pattern) =>
        ComposerItem.From(MakeStep(type, pattern));

    [Fact]
    public void Build_EmptyList_ReturnsEmptyString()
    {
        var result = FeatureTextBuilder.Build("My Scenario", []);
        Assert.Equal("", result);
    }

    [Fact]
    public void Build_BlankName_UsesUntitledScenario()
    {
        var result = FeatureTextBuilder.Build("", [Item("Given", "something")]);
        Assert.Contains("Untitled scenario", result);
    }

    [Fact]
    public void Build_SingleGiven_UsesGivenKeyword()
    {
        var result = FeatureTextBuilder.Build("Login", [Item("Given", "I am logged in")]);
        Assert.Contains("    Given I am logged in", result);
    }

    [Fact]
    public void Build_TwoConsecutiveGivens_SecondUsesAnd()
    {
        ComposerItem[] items = [Item("Given", "I am logged in"), Item("Given", "I have a token")];
        var result = FeatureTextBuilder.Build("Test", items);
        Assert.Contains("    Given I am logged in", result);
        Assert.Contains("    And I have a token", result);
    }

    [Fact]
    public void Build_DifferentTypes_UsesCorrectKeywords_NoAnd()
    {
        ComposerItem[] items = [Item("Given", "I am logged in"), Item("When", "I request the page"), Item("Then", "I see a 200")];
        var result = FeatureTextBuilder.Build("Test", items);
        Assert.Contains("    Given I am logged in", result);
        Assert.Contains("    When I request the page", result);
        Assert.Contains("    Then I see a 200", result);
        Assert.DoesNotContain("And", result);
    }

    [Fact]
    public void Build_RepeatedTypeThenDifferent_ProducesCorrectKeywords()
    {
        ComposerItem[] items = [Item("Given", "step a"), Item("Given", "step b"), Item("When", "step c"), Item("When", "step d")];
        var result = FeatureTextBuilder.Build("Test", items);
        var lines = result.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        Assert.Contains("    Given step a", lines);
        Assert.Contains("    And step b",   lines);
        Assert.Contains("    When step c",  lines);
        Assert.Contains("    And step d",   lines);
    }

    [Fact]
    public void Build_IncludesFeatureAndScenarioHeaders()
    {
        var result = FeatureTextBuilder.Build("My Test", [Item("Given", "something")]);
        Assert.Contains("Feature: My Test", result);
        Assert.Contains("  Scenario: My Test", result);
    }

    [Fact]
    public void GetKeyword_FirstItem_ReturnsType()
    {
        ComposerItem[] items = [Item("Given", "step a")];
        Assert.Equal("Given", FeatureTextBuilder.GetKeyword(0, items));
    }

    [Fact]
    public void GetKeyword_ConsecutiveSameType_ReturnsAnd()
    {
        ComposerItem[] items = [Item("Given", "step a"), Item("Given", "step b")];
        Assert.Equal("And", FeatureTextBuilder.GetKeyword(1, items));
    }

    [Fact]
    public void GetKeyword_TypeChange_ReturnsNewType()
    {
        ComposerItem[] items = [Item("Given", "step a"), Item("When", "step b")];
        Assert.Equal("When", FeatureTextBuilder.GetKeyword(1, items));
    }

    private static ComposerItem ItemWithValues(string type, string pattern, params string[] values) =>
        new(Guid.NewGuid(), MakeStep(type, pattern), values);

    [Fact]
    public void Build_SubstitutesConcreteValues()
    {
        var item = ItemWithValues("Given", "I am logged in as {username : string}", "admin");
        var result = FeatureTextBuilder.Build("Test", [item]);
        Assert.Contains("Given I am logged in as admin", result);
    }

    [Fact]
    public void Build_EmptyValue_EmitsEmptyString()
    {
        // Two items so the empty-param line is not the last line (TrimEnd would strip trailing space)
        var itemWithEmpty = ItemWithValues("Given", "I am logged in as {username : string}", "");
        var itemAfter = ItemWithValues("When", "I do something");
        var result = FeatureTextBuilder.Build("Test", [itemWithEmpty, itemAfter]);
        Assert.Contains("Given I am logged in as ", result);
    }

    [Fact]
    public void Build_NoParams_RendersPatternVerbatim()
    {
        var item = ItemWithValues("Given", "the system is ready");
        var result = FeatureTextBuilder.Build("Test", [item]);
        Assert.Contains("Given the system is ready", result);
    }

    [Fact]
    public void Build_MoreTokensThanParamValues_ExtraTokensEmpty()
    {
        // Two items so the line with empty trailing param is not the last (TrimEnd strips trailing space)
        var item = ItemWithValues("Given", "from {src : string} to {dst : string}", "London");
        var itemAfter = ItemWithValues("When", "I continue");
        var result = FeatureTextBuilder.Build("Test", [item, itemAfter]);
        Assert.Contains("Given from London to ", result);
    }

    // ── AppendTableRows ──────────────────────────────────────────────────────

    [Fact]
    public void AppendTableRows_ZeroColumns_ReturnsEmpty()
        => Assert.Equal("", FeatureTextBuilder.AppendTableRows(Array.Empty<string>()));

    [Fact]
    public void AppendTableRows_SingleColumn_ProducesHeaderAndEmptyRow()
    {
        var result = FeatureTextBuilder.AppendTableRows(new[] { "Name" });
        Assert.Contains("| Name |", result);
        Assert.Equal(2, result.Split('\n').Length - 1); // leading \n + header + empty row
    }

    [Fact]
    public void AppendTableRows_MultipleColumns_AllColumnsInHeader()
    {
        var result = FeatureTextBuilder.AppendTableRows(new[] { "Col1", "Col2", "Col3" });
        Assert.Contains("| Col1 | Col2 | Col3 |", result);
    }

    [Fact]
    public void AppendTableRows_EmptyDataRow_HasPaddedCells()
    {
        var result = FeatureTextBuilder.AppendTableRows(new[] { "A", "B" });
        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // Second line is the empty data row; each empty cell should have at least one space
        Assert.Contains("|  ", lines[^1]);
    }

    [Fact]
    public void AppendTableRows_CustomIndent_AppliedToAllRows()
    {
        var result = FeatureTextBuilder.AppendTableRows(new[] { "X" }, "  ");
        foreach (var line in result.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            Assert.StartsWith("  |", line);
    }

    [Fact]
    public void Build_StepWithTableParam_IncludesTableBlock()
    {
        var step = new Step
        {
            Id = "s1", Type = "Given",
            Pattern = "the following trades exist",
            Domain = "Core", File = "T.cs", Line = 1,
            Params =
            [
                new StepParam
                {
                    Name = "trades", Type = "table",
                    ColumnsSource = "declared",
                    Columns = [new TableColumn { Name = "Notional", Type = "string" },
                               new TableColumn { Name = "Currency", Type = "string" }]
                }
            ]
        };
        var item = ComposerItem.From(step);
        var result = FeatureTextBuilder.Build("My Scenario", [item]);
        Assert.Contains("| Notional | Currency |", result);
        Assert.Contains("Given the following trades exist", result);
    }
}
