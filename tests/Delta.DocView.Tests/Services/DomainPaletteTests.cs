using Delta.DocView.Client.Services;

namespace Delta.DocView.Tests.Services;

public class DomainPaletteTests
{
    [Fact]
    public void IndexFor_IsDeterministic_ForSameId()
    {
        var first = DomainPalette.IndexFor("auth");
        var second = DomainPalette.IndexFor("auth");

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("auth")]
    [InlineData("storage")]
    [InlineData("networking")]
    [InlineData("identity")]
    [InlineData("a")]
    [InlineData("some-very-long-domain-id-12345")]
    public void IndexFor_ReturnsValueInPaletteRange(string id)
    {
        var idx = DomainPalette.IndexFor(id);

        Assert.InRange(idx, 0, DomainPalette.PaletteSize - 1);
    }

    [Fact]
    public void IndexFor_EmptyString_ReturnsZero()
    {
        Assert.Equal(0, DomainPalette.IndexFor(""));
    }

    [Fact]
    public void IndexFor_Null_ReturnsZero()
    {
        Assert.Equal(0, DomainPalette.IndexFor(null!));
    }

    [Fact]
    public void CssVarValue_IsHslString_ConsistentWithIndexFor()
    {
        var css = DomainPalette.CssVarValue("auth");

        Assert.StartsWith("hsl(", css);
        // Calling twice yields the same palette entry.
        Assert.Equal(css, DomainPalette.CssVarValue("auth"));
    }

    [Fact]
    public void CssVarName_Lowercases_SimpleId()
    {
        Assert.Equal("--dom-auth", DomainPalette.CssVarName("Auth"));
    }

    [Fact]
    public void CssVarName_ReplacesWhitespace()
    {
        Assert.Equal("--dom-order-mgmt", DomainPalette.CssVarName("Order Mgmt"));
    }

    [Fact]
    public void CssVarName_ReplacesPunctuation()
    {
        Assert.Equal("--dom-foo-bar", DomainPalette.CssVarName("foo!bar"));
    }

    [Fact]
    public void CssVarName_EmptyString_ReturnsDefault()
    {
        Assert.Equal("--dom-default", DomainPalette.CssVarName(""));
    }

    [Fact]
    public void CssVarName_Null_ReturnsDefault()
    {
        Assert.Equal("--dom-default", DomainPalette.CssVarName(null!));
    }
}
