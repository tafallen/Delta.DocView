using Delta.DocView.Client.Services;

namespace Delta.DocView.Tests.Services;

public class DomainPaletteTests
{
    [Fact]
    public void HueFor_IsDeterministic_ForSameId()
    {
        var first = DomainPalette.HueFor("auth");
        var second = DomainPalette.HueFor("auth");

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("auth")]
    [InlineData("storage")]
    [InlineData("networking")]
    [InlineData("identity")]
    [InlineData("a")]
    [InlineData("some-very-long-domain-id-12345")]
    public void HueFor_ReturnsValueInRange(string id)
    {
        var hue = DomainPalette.HueFor(id);

        Assert.InRange(hue, 0, 359);
    }

    [Fact]
    public void HueFor_ProducesGenerallyDistinctValues_AcrossDifferentIds()
    {
        var ids = new[] { "auth", "storage", "networking", "identity", "compute" };
        var distinct = ids.Select(DomainPalette.HueFor).Distinct().Count();

        Assert.True(distinct >= 3, $"Expected at least 3 distinct hues, got {distinct}");
    }

    [Fact]
    public void HueFor_EmptyString_ReturnsZero()
    {
        Assert.Equal(0, DomainPalette.HueFor(""));
    }

    [Fact]
    public void HueFor_Null_ReturnsZero()
    {
        Assert.Equal(0, DomainPalette.HueFor(null!));
    }

    [Fact]
    public void CssVarValue_MatchesHueForSameId()
    {
        var hue = DomainPalette.HueFor("auth");
        var css = DomainPalette.CssVarValue("auth");

        Assert.Equal($"hsl({hue} 60% 45%)", css);
    }
}
