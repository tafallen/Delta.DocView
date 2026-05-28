using AngleSharp.Dom;
using Bunit;
using Delta.DocView.Client.Components;

namespace Delta.DocView.Tests.Components;

public class HighlightRendererTests : TestContext
{
    [Fact]
    public void NullQuery_RendersVerbatim_NoMark()
    {
        var cut = RenderComponent<HighlightRenderer>(p => p
            .Add(c => c.Text, "Hello world")
            .Add(c => c.Query, null));

        Assert.Empty(cut.FindAll("mark"));
        Assert.Contains("Hello world", cut.Markup);
    }

    [Fact]
    public void WhitespaceQuery_RendersVerbatim_NoMark()
    {
        var cut = RenderComponent<HighlightRenderer>(p => p
            .Add(c => c.Text, "Hello world")
            .Add(c => c.Query, "   "));

        Assert.Empty(cut.FindAll("mark"));
        Assert.Contains("Hello world", cut.Markup);
    }

    [Fact]
    public void EmptyText_RendersNothing()
    {
        var cut = RenderComponent<HighlightRenderer>(p => p
            .Add(c => c.Text, "")
            .Add(c => c.Query, "hello"));

        Assert.Empty(cut.FindAll("mark"));
        Assert.Equal(string.Empty, cut.Markup.Trim());
    }

    [Fact]
    public void SingleMatch_WrapsInMark_PreservesOriginalCasing()
    {
        var cut = RenderComponent<HighlightRenderer>(p => p
            .Add(c => c.Text, "I am Logged in")
            .Add(c => c.Query, "log"));

        var marks = cut.FindAll("mark");
        Assert.Single(marks);
        Assert.Equal("Log", marks[0].TextContent);
    }

    [Fact]
    public void MultipleMatches_RendersAllInOrder()
    {
        var cut = RenderComponent<HighlightRenderer>(p => p
            .Add(c => c.Text, "foo bar foo baz foo")
            .Add(c => c.Query, "foo"));

        var marks = cut.FindAll("mark");
        Assert.Equal(3, marks.Count);
        Assert.All(marks, m => Assert.Equal("foo", m.TextContent));
    }

    [Fact]
    public void CaseInsensitive_PreservesEachOriginalCasing()
    {
        var cut = RenderComponent<HighlightRenderer>(p => p
            .Add(c => c.Text, "HELLO hello HeLLo")
            .Add(c => c.Query, "hello"));

        var marks = cut.FindAll("mark");
        Assert.Equal(3, marks.Count);
        Assert.Equal("HELLO", marks[0].TextContent);
        Assert.Equal("hello", marks[1].TextContent);
        Assert.Equal("HeLLo", marks[2].TextContent);
    }

    [Fact]
    public void NoMatch_NoMark()
    {
        var cut = RenderComponent<HighlightRenderer>(p => p
            .Add(c => c.Text, "Hello world")
            .Add(c => c.Query, "xyz"));

        Assert.Empty(cut.FindAll("mark"));
        Assert.Contains("Hello world", cut.Markup);
    }
}
