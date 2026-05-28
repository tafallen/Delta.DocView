using Delta.DocView.Client.Services;

namespace Delta.DocView.Tests.Services;

public class CSharpHighlighterTests
{
    [Fact]
    public void Empty_Source_Returns_Empty()
    {
        Assert.Empty(CSharpHighlighter.Tokenise(""));
    }

    [Fact]
    public void Keyword_Identified()
    {
        var tokens = CSharpHighlighter.Tokenise("public void Foo()");

        Assert.Contains(tokens, t => t.Text == "public" && t.CssClass == "cs-kw");
        Assert.Contains(tokens, t => t.Text == "void" && t.CssClass == "cs-kw");
        Assert.Contains(tokens, t => t.Text == "Foo" && t.CssClass == "cs-text");
    }

    [Fact]
    public void String_Identified()
    {
        var tokens = CSharpHighlighter.Tokenise("Console.WriteLine(\"hi\")");

        var strTokens = tokens.Where(t => t.CssClass == "cs-string").ToList();
        var strToken = Assert.Single(strTokens);
        Assert.Equal("\"hi\"", strToken.Text);
    }

    [Fact]
    public void Verbatim_String_Identified()
    {
        var tokens = CSharpHighlighter.Tokenise("@\"C:\\path\"");

        Assert.Contains(tokens, t => t.CssClass == "cs-string");
    }

    [Fact]
    public void Line_Comment_Identified()
    {
        var tokens = CSharpHighlighter.Tokenise("// hello");

        var comments = tokens.Where(t => t.CssClass == "cs-comment").ToList();
        var comment = Assert.Single(comments);
        Assert.Equal("// hello", comment.Text);
    }

    [Fact]
    public void Block_Comment_Spans_Multiple_Lines()
    {
        var src = "/* line one\nline two */";
        var tokens = CSharpHighlighter.Tokenise(src);

        var comments = tokens.Where(t => t.CssClass == "cs-comment").ToList();
        var comment = Assert.Single(comments);
        Assert.Equal(src, comment.Text);
    }

    [Fact]
    public void Number_Identified()
    {
        var tokens = CSharpHighlighter.Tokenise("return 42;");

        Assert.Contains(tokens, t => t.Text == "42" && t.CssClass == "cs-number");
    }

    [Fact]
    public void Identifier_Not_Keyword_Is_Text()
    {
        var tokens = CSharpHighlighter.Tokenise("customer");

        var token = Assert.Single(tokens);
        Assert.Equal("customer", token.Text);
        Assert.Equal("cs-text", token.CssClass);
    }

    [Fact]
    public void Roundtrip_Preserves_All_Characters()
    {
        var src = "public class Foo\n{\n    // a comment\n    public string Name => \"bar\";\n    public int Count = 42;\n}\n";

        var tokens = CSharpHighlighter.Tokenise(src);
        var roundtripped = string.Concat(tokens.Select(t => t.Text));

        Assert.Equal(src, roundtripped);
    }
}
