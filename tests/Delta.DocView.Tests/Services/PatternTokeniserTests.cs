using Delta.DocView.Client.Services;

namespace Delta.DocView.Tests.Services;

public class PatternTokeniserTests
{
    [Fact]
    public void Plain_Text_Returns_Single_StaticText()
    {
        var tokens = PatternTokeniser.Tokenise("hello world");
        var single = Assert.Single(tokens);
        var st = Assert.IsType<StaticText>(single);
        Assert.Equal("hello world", st.Text);
    }

    [Fact]
    public void Type_Only_Token()
    {
        var tokens = PatternTokeniser.Tokenise("{string}");
        var single = Assert.Single(tokens);
        var pt = Assert.IsType<ParamToken>(single);
        Assert.Null(pt.Name);
        Assert.Equal("string", pt.Type);
    }

    [Fact]
    public void Name_And_Type_Token()
    {
        var tokens = PatternTokeniser.Tokenise("{name : string}");
        var single = Assert.Single(tokens);
        var pt = Assert.IsType<ParamToken>(single);
        Assert.Equal("name", pt.Name);
        Assert.Equal("string", pt.Type);
    }

    [Fact]
    public void Empty_Token_Renders_As_Literal_StaticText()
    {
        var tokens = PatternTokeniser.Tokenise("{}");
        var single = Assert.Single(tokens);
        var st = Assert.IsType<StaticText>(single);
        Assert.Equal("{}", st.Text);
    }

    [Fact]
    public void Colon_Only_Token()
    {
        var tokens = PatternTokeniser.Tokenise("{:type}");
        var single = Assert.Single(tokens);
        var pt = Assert.IsType<ParamToken>(single);
        Assert.Null(pt.Name);
        Assert.Equal("type", pt.Type);
    }

    [Fact]
    public void Empty_Type_Token_Renders_As_Literal_StaticText()
    {
        var tokens = PatternTokeniser.Tokenise("{name:}");
        var single = Assert.Single(tokens);
        var st = Assert.IsType<StaticText>(single);
        Assert.Equal("{name:}", st.Text);
    }

    [Fact]
    public void Multi_Colon_Token_Splits_On_First()
    {
        var tokens = PatternTokeniser.Tokenise("{a:b:c}");
        var single = Assert.Single(tokens);
        var pt = Assert.IsType<ParamToken>(single);
        Assert.Equal("a", pt.Name);
        Assert.Equal("b:c", pt.Type);
    }

    [Fact]
    public void Interleaved_Static_And_Tokens_In_Order()
    {
        var tokens = PatternTokeniser.Tokenise("add {a : int} and {b : int}");
        Assert.Equal(4, tokens.Count);

        var s1 = Assert.IsType<StaticText>(tokens[0]);
        Assert.Equal("add ", s1.Text);

        var p1 = Assert.IsType<ParamToken>(tokens[1]);
        Assert.Equal("a", p1.Name);
        Assert.Equal("int", p1.Type);

        var s2 = Assert.IsType<StaticText>(tokens[2]);
        Assert.Equal(" and ", s2.Text);

        var p2 = Assert.IsType<ParamToken>(tokens[3]);
        Assert.Equal("b", p2.Name);
        Assert.Equal("int", p2.Type);
    }
}
