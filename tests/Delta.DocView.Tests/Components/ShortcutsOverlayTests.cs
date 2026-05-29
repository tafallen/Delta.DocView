using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Delta.DocView.Tests.Components;

public class ShortcutsOverlayTests
{
    private static (TestContext ctx, ShortcutsState state) Setup(IPlatform? platform = null)
    {
        var ctx = new TestContext();
        var p = platform ?? Substitute.For<IPlatform>();
        if (platform is null) p.ShortcutLabel("K").Returns("Ctrl+K");

        ctx.Services.AddSingleton(Substitute.For<IKeyboardActions>());
        ctx.Services.AddScoped<ShortcutsState>();
        ctx.Services.AddSingleton(p);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var state = ctx.Services.GetRequiredService<ShortcutsState>();
        return (ctx, state);
    }

    [Fact]
    public void Renders_Nothing_When_Closed()
    {
        var (ctx, _) = Setup();

        var cut = ctx.RenderComponent<ShortcutsOverlay>();

        Assert.Empty(cut.FindAll("[data-testid='shortcuts-dialog']"));
    }

    [Fact]
    public void Renders_Dialog_When_Open()
    {
        var (ctx, state) = Setup();
        var cut = ctx.RenderComponent<ShortcutsOverlay>();

        state.Open();

        var dialog = cut.Find("[data-testid='shortcuts-dialog']");
        Assert.Equal("dialog", dialog.GetAttribute("role"));
        Assert.Equal("true", dialog.GetAttribute("aria-modal"));
    }

    [Fact]
    public void Renders_One_Row_Per_Shortcut()
    {
        var (ctx, state) = Setup();
        var cut = ctx.RenderComponent<ShortcutsOverlay>();

        state.Open();

        Assert.Equal(7, cut.FindAll("[data-testid='shortcut-row']").Count);
    }

    [Fact]
    public void Palette_Row_Shows_Two_Kbd_Chips()
    {
        var (ctx, state) = Setup();
        var cut = ctx.RenderComponent<ShortcutsOverlay>();

        state.Open();

        var firstRow = cut.FindAll("[data-testid='shortcut-row']")[0];
        Assert.Equal(2, firstRow.QuerySelectorAll("kbd").Length);
        Assert.Contains("or", firstRow.TextContent);
    }

    [Fact]
    public void Mac_Palette_Row_Shows_CommandK()
    {
        var platform = Substitute.For<IPlatform>();
        platform.IsMac.Returns(true);
        platform.ShortcutLabel("K").Returns("⌘K");
        var (ctx, state) = Setup(platform);
        var cut = ctx.RenderComponent<ShortcutsOverlay>();

        state.Open();

        var firstRow = cut.FindAll("[data-testid='shortcut-row']")[0];
        Assert.Contains("⌘K", firstRow.TextContent);
    }

    [Fact]
    public void NonMac_Palette_Row_Shows_CtrlK()
    {
        var platform = Substitute.For<IPlatform>();
        platform.IsMac.Returns(false);
        platform.ShortcutLabel("K").Returns("Ctrl+K");
        var (ctx, state) = Setup(platform);
        var cut = ctx.RenderComponent<ShortcutsOverlay>();

        state.Open();

        var firstRow = cut.FindAll("[data-testid='shortcut-row']")[0];
        Assert.Contains("Ctrl+K", firstRow.TextContent);
    }

    [Fact]
    public void Escape_On_Dialog_Closes()
    {
        var (ctx, state) = Setup();
        var cut = ctx.RenderComponent<ShortcutsOverlay>();
        state.Open();

        cut.Find("[data-testid='shortcuts-dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll("[data-testid='shortcuts-dialog']"));
    }

    [Fact]
    public void Backdrop_Click_Closes()
    {
        var (ctx, state) = Setup();
        var cut = ctx.RenderComponent<ShortcutsOverlay>();
        state.Open();

        cut.Find("[data-testid='shortcuts-backdrop']").Click();

        Assert.Empty(cut.FindAll("[data-testid='shortcuts-dialog']"));
    }

    [Fact]
    public void Close_Button_Closes()
    {
        var (ctx, state) = Setup();
        var cut = ctx.RenderComponent<ShortcutsOverlay>();
        state.Open();

        cut.Find("[data-testid='shortcuts-close']").Click();

        Assert.Empty(cut.FindAll("[data-testid='shortcuts-dialog']"));
    }
}
