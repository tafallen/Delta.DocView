using Bunit;
using Delta.DocView.Client.Components;

namespace Delta.DocView.Tests.Components;

public class PatternRendererTests : TestContext
{
    [Fact]
    public void NoTokens_RendersPlainText_NoPill()
    {
        var cut = RenderComponent<PatternRenderer>(p => p
            .Add(c => c.Pattern, "plain text only"));

        Assert.Empty(cut.FindAll(".param-pill"));
        Assert.Contains("plain text only", cut.Markup);
    }

    [Fact]
    public void TypeOnlyToken_RendersSinglePill_NoName()
    {
        var cut = RenderComponent<PatternRenderer>(p => p
            .Add(c => c.Pattern, "{string}"));

        var pills = cut.FindAll(".param-pill");
        Assert.Single(pills);
        Assert.Contains("param-string", pills[0].ClassList);
        Assert.Empty(cut.FindAll(".param-name"));
        var type = cut.Find(".param-type");
        Assert.Equal("string", type.TextContent);
    }

    [Fact]
    public void NamedToken_RendersNameAndType_WithLeadingText()
    {
        var cut = RenderComponent<PatternRenderer>(p => p
            .Add(c => c.Pattern, "I am logged in as {username : string}"));

        Assert.Contains("I am logged in as", cut.Markup);
        var pills = cut.FindAll(".param-pill");
        Assert.Single(pills);
        Assert.Contains("param-string", pills[0].ClassList);
        Assert.Equal("username", cut.Find(".param-name").TextContent);
        Assert.Equal("string", cut.Find(".param-type").TextContent);
    }

    [Fact]
    public void MultipleTokens_RenderedInOrder_WithBetweenText()
    {
        var cut = RenderComponent<PatternRenderer>(p => p
            .Add(c => c.Pattern, "add {a : int} and {b : int}"));

        var pills = cut.FindAll(".param-pill");
        Assert.Equal(2, pills.Count);

        var names = cut.FindAll(".param-name");
        Assert.Equal(2, names.Count);
        Assert.Equal("a", names[0].TextContent);
        Assert.Equal("b", names[1].TextContent);

        var types = cut.FindAll(".param-type");
        Assert.Equal(2, types.Count);
        Assert.Equal("int", types[0].TextContent);
        Assert.Equal("int", types[1].TextContent);

        Assert.Contains("param-int", pills[0].ClassList);
        Assert.Contains("param-int", pills[1].ClassList);
        Assert.Contains("and", cut.Markup);
    }

    [Fact]
    public void Query_HighlightsOnlyInStaticText_NotInsidePill()
    {
        var cut = RenderComponent<PatternRenderer>(p => p
            .Add(c => c.Pattern, "I am logged in as {string}")
            .Add(c => c.Query, "in"));

        var marks = cut.FindAll("mark");
        Assert.NotEmpty(marks);
        Assert.All(marks, m => Assert.Equal("in", m.TextContent));

        var pill = cut.Find(".param-pill");
        Assert.Empty(pill.QuerySelectorAll("mark"));
        Assert.Equal("string", cut.Find(".param-type").TextContent);
    }
}
