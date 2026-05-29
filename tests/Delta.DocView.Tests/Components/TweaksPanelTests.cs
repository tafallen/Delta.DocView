using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Delta.DocView.Tests.Components;

public class TweaksPanelTests
{
    private static (TestContext ctx, TweaksStore store, TweaksPanelState panel) Setup()
    {
        var ctx = new TestContext();
        ctx.Services.AddScoped<TweaksStore>();
        ctx.Services.AddScoped<TweaksPanelState>();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var store = ctx.Services.GetRequiredService<TweaksStore>();
        var panel = ctx.Services.GetRequiredService<TweaksPanelState>();
        return (ctx, store, panel);
    }

    [Fact]
    public void Renders_Nothing_When_Closed()
    {
        var (ctx, _, _) = Setup();

        var cut = ctx.RenderComponent<TweaksPanel>();

        Assert.Empty(cut.FindAll("[data-testid='tweaks-panel']"));
    }

    [Fact]
    public void Renders_When_Open()
    {
        var (ctx, _, panel) = Setup();
        var cut = ctx.RenderComponent<TweaksPanel>();

        panel.Open();

        var dialog = cut.Find("[data-testid='tweaks-panel']");
        Assert.Equal("dialog", dialog.GetAttribute("role"));
        Assert.Equal("true", dialog.GetAttribute("aria-modal"));
    }

    [Fact]
    public void Dark_Toggle_Calls_SetDark()
    {
        var (ctx, store, panel) = Setup();
        var cut = ctx.RenderComponent<TweaksPanel>();
        panel.Open();

        cut.Find("[data-testid='tweaks-dark']").Change(true);

        Assert.True(store.Dark);
    }

    [Fact]
    public void Accent_Blue_Radio_Sets_Accent_Blue()
    {
        var (ctx, store, panel) = Setup();
        var cut = ctx.RenderComponent<TweaksPanel>();
        panel.Open();

        cut.Find("[data-testid='tweaks-accent-blue']").Change(true);

        Assert.Equal(AccentOption.Blue, store.Accent);
    }

    [Fact]
    public void Density_Compact_Radio_Sets_Density()
    {
        var (ctx, store, panel) = Setup();
        var cut = ctx.RenderComponent<TweaksPanel>();
        panel.Open();

        cut.Find("[data-testid='tweaks-density-compact']").Change(true);

        Assert.Equal(DensityOption.Compact, store.Density);
    }

    [Fact]
    public void RowEmphasis_Meta_Radio_Sets_RowEmphasis()
    {
        var (ctx, store, panel) = Setup();
        var cut = ctx.RenderComponent<TweaksPanel>();
        panel.Open();

        cut.Find("[data-testid='tweaks-row-meta']").Change(true);

        Assert.Equal(RowEmphasisOption.Meta, store.RowEmphasis);
    }

    [Fact]
    public void Source_Expanded_Radio_Sets_SourceDefault()
    {
        var (ctx, store, panel) = Setup();
        var cut = ctx.RenderComponent<TweaksPanel>();
        panel.Open();

        cut.Find("[data-testid='tweaks-source-expanded']").Change(true);

        Assert.Equal(SourceDefaultOption.Expanded, store.SourceDefault);
    }

    [Fact]
    public void Escape_Closes()
    {
        var (ctx, _, panel) = Setup();
        var cut = ctx.RenderComponent<TweaksPanel>();
        panel.Open();

        cut.Find("[data-testid='tweaks-panel']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll("[data-testid='tweaks-panel']"));
    }

    [Fact]
    public void Backdrop_Click_Closes()
    {
        var (ctx, _, panel) = Setup();
        var cut = ctx.RenderComponent<TweaksPanel>();
        panel.Open();

        cut.Find("[data-testid='tweaks-backdrop']").Click();

        Assert.Empty(cut.FindAll("[data-testid='tweaks-panel']"));
    }

    [Fact]
    public void Selected_Accent_Radio_Reflects_Store()
    {
        var (ctx, store, panel) = Setup();
        store.SetAccent(AccentOption.Blue);
        var cut = ctx.RenderComponent<TweaksPanel>();

        panel.Open();

        var blue = cut.Find("[data-testid='tweaks-accent-blue']");
        Assert.True(blue.HasAttribute("checked"));
    }
}
