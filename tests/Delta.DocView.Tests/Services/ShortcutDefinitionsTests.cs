using Delta.DocView.Client.Services;
using NSubstitute;

namespace Delta.DocView.Tests.Services;

public class ShortcutDefinitionsTests
{
    [Fact]
    public void For_Mac_PaletteBinding_UsesCommandK()
    {
        var platform = Substitute.For<IPlatform>();
        platform.ShortcutLabel("K").Returns("⌘K");

        var shortcuts = ShortcutDefinitions.For(platform);

        Assert.Equal(new[] { "⌘K", "/" }, shortcuts[0].Keys);
    }

    [Fact]
    public void For_NonMac_PaletteBinding_UsesCtrlK()
    {
        var platform = Substitute.For<IPlatform>();
        platform.ShortcutLabel("K").Returns("Ctrl+K");

        var shortcuts = ShortcutDefinitions.For(platform);

        Assert.Equal(new[] { "Ctrl+K", "/" }, shortcuts[0].Keys);
    }

    [Fact]
    public void For_Returns_Seven_Shortcuts()
    {
        var platform = Substitute.For<IPlatform>();
        platform.ShortcutLabel("K").Returns("Ctrl+K");

        Assert.Equal(7, ShortcutDefinitions.For(platform).Count);
    }

    [Fact]
    public void For_Labels_In_Documented_Order()
    {
        var platform = Substitute.For<IPlatform>();
        platform.ShortcutLabel("K").Returns("Ctrl+K");

        var labels = ShortcutDefinitions.For(platform).Select(s => s.Label).ToArray();

        Assert.Equal(new[]
        {
            "Open command palette",
            "Show keyboard shortcuts",
            "Toggle scenario composer",
            "Toggle favourite on selected step",
            "Move selection down",
            "Move selection up",
            "Close overlay",
        }, labels);
    }

    [Fact]
    public void For_ShowShortcuts_Row_Has_QuestionMark()
    {
        var platform = Substitute.For<IPlatform>();
        platform.ShortcutLabel("K").Returns("Ctrl+K");

        var shortcut = ShortcutDefinitions.For(platform)
            .Single(s => s.Label == "Show keyboard shortcuts");

        Assert.Equal(new[] { "?" }, shortcut.Keys);
    }
}
