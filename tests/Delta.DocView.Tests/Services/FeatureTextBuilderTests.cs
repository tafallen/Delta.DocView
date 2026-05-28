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
}
