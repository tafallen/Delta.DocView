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

    [Fact]
    public void Raw_String_Not_Recognised_As_Single_Literal()
    {
        // Raw string literals ("""...""") are NOT recognised as a single token.
        // The regular-string alternative greedily matches adjacent quote pairs:
        // """raw""" tokenises as ["" (empty cs-string), "raw" (cs-string),
        // "" (empty cs-string)] — three separate tokens rather than one.
        var tokens = CSharpHighlighter.Tokenise("\"\"\"raw\"\"\"");

        // No single cs-string token spans the entire triple-quoted literal.
        Assert.DoesNotContain(tokens, t => t.CssClass == "cs-string" && t.Text == "\"\"\"raw\"\"\"");
        // The "raw" content gets wrapped in adjacent quote pairs, classed cs-string
        // — the misclassification a future fix would address.
        Assert.Contains(tokens, t => t.Text == "\"raw\"" && t.CssClass == "cs-string");
    }

    [Fact]
    public void Modern_Keyword_Init_Not_Highlighted()
    {
        var tokens = CSharpHighlighter.Tokenise("public int Foo { get; init; }");

        Assert.DoesNotContain(tokens, t => t.Text == "init" && t.CssClass == "cs-kw");
        Assert.Contains(tokens, t => t.Text == "init" && t.CssClass == "cs-text");
    }

    [Fact]
    public void Hex_Literal_Falls_Back()
    {
        var tokens = CSharpHighlighter.Tokenise("var x = 0xFF;");

        // 0xFF is not recognised as a single number; it splits into "0" (cs-number)
        // and "xFF" (cs-text identifier).
        Assert.DoesNotContain(tokens, t => t.Text == "0xFF" && t.CssClass == "cs-number");
        Assert.DoesNotContain(tokens, t => t.Text == "0xFF");
    }

    [Fact]
    public void Float_Literal_Falls_Back()
    {
        var tokens = CSharpHighlighter.Tokenise("var x = 1.5;");

        // No single token captures "1.5"; the dot splits it.
        Assert.DoesNotContain(tokens, t => t.Text == "1.5");
        Assert.Contains(tokens, t => t.Text == "1" && t.CssClass == "cs-number");
    }

    [Fact]
    public void Comment_Wins_Over_Embedded_String()
    {
        var src = "// hello \"world\"";
        var tokens = CSharpHighlighter.Tokenise(src);

        var comments = tokens.Where(t => t.CssClass == "cs-comment").ToList();
        var comment = Assert.Single(comments);
        Assert.Equal(src, comment.Text);
        Assert.DoesNotContain(tokens, t => t.CssClass == "cs-string");
    }

    [Fact]
    public void String_Wins_Over_Embedded_Comment_Like()
    {
        var src = "\"// not a comment\"";
        var tokens = CSharpHighlighter.Tokenise(src);

        var strings = tokens.Where(t => t.CssClass == "cs-string").ToList();
        var str = Assert.Single(strings);
        Assert.Equal(src, str.Text);
        Assert.DoesNotContain(tokens, t => t.CssClass == "cs-comment");
    }

    [Fact]
    public void Block_Comment_Containing_String_Delimiter()
    {
        var src = "/* a \"b\" c */";
        var tokens = CSharpHighlighter.Tokenise(src);

        var comments = tokens.Where(t => t.CssClass == "cs-comment").ToList();
        var comment = Assert.Single(comments);
        Assert.Equal(src, comment.Text);
        Assert.DoesNotContain(tokens, t => t.CssClass == "cs-string");
    }
}
