using Delta.DocView.Client.Services;
using Microsoft.JSInterop;
using NSubstitute;

namespace Delta.DocView.Tests.Services;

public class PlatformServiceTests
{
    [Fact]
    public void Default_IsMac_False()
    {
        var js = Substitute.For<IJSRuntime>();
        var svc = new PlatformService(js);

        Assert.False(svc.IsMac);
    }

    [Fact]
    public async Task InitializeAsync_TrueFromJs_SetsIsMacTrue()
    {
        var js = Substitute.For<IJSRuntime>();
        js.InvokeAsync<bool>("docview.platform.isMac", Arg.Any<object?[]?>())
            .Returns(ValueTask.FromResult(true));

        var svc = new PlatformService(js);
        await svc.InitializeAsync();

        Assert.True(svc.IsMac);
    }

    [Fact]
    public async Task InitializeAsync_FalseFromJs_SetsIsMacFalse()
    {
        var js = Substitute.For<IJSRuntime>();
        js.InvokeAsync<bool>("docview.platform.isMac", Arg.Any<object?[]?>())
            .Returns(ValueTask.FromResult(false));

        var svc = new PlatformService(js);
        await svc.InitializeAsync();

        Assert.False(svc.IsMac);
    }

    [Fact]
    public async Task InitializeAsync_JsThrows_LeavesIsMacFalse_NoThrow()
    {
        var js = Substitute.For<IJSRuntime>();
        js.InvokeAsync<bool>("docview.platform.isMac", Arg.Any<object?[]?>())
            .Returns<ValueTask<bool>>(_ => throw new JSException("simulated JS failure"));

        var svc = new PlatformService(js);
        await svc.InitializeAsync();

        Assert.False(svc.IsMac);
    }

    [Fact]
    public async Task InitializeAsync_SecondCall_NoOps()
    {
        var js = Substitute.For<IJSRuntime>();
        js.InvokeAsync<bool>("docview.platform.isMac", Arg.Any<object?[]?>())
            .Returns(ValueTask.FromResult(true));

        var svc = new PlatformService(js);
        await svc.InitializeAsync();
        await svc.InitializeAsync();

        await js.Received(1).InvokeAsync<bool>("docview.platform.isMac", Arg.Any<object?[]?>());
    }

    [Fact]
    public async Task ShortcutLabel_K_Mac_ReturnsCommandK()
    {
        var js = Substitute.For<IJSRuntime>();
        js.InvokeAsync<bool>("docview.platform.isMac", Arg.Any<object?[]?>())
            .Returns(ValueTask.FromResult(true));

        var svc = new PlatformService(js);
        await svc.InitializeAsync();

        Assert.Equal("⌘K", svc.ShortcutLabel("K"));
    }

    [Fact]
    public async Task ShortcutLabel_K_NonMac_ReturnsCtrlK()
    {
        var js = Substitute.For<IJSRuntime>();
        js.InvokeAsync<bool>("docview.platform.isMac", Arg.Any<object?[]?>())
            .Returns(ValueTask.FromResult(false));

        var svc = new PlatformService(js);
        await svc.InitializeAsync();

        Assert.Equal("Ctrl+K", svc.ShortcutLabel("K"));
    }
}
