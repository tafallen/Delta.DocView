using Bunit;
using Delta.DocView.Client.Components;

namespace Delta.DocView.Tests.Components;

public class LoadingScreenTests : TestContext
{
    [Fact]
    public void LoadingScreen_RendersAppName()
    {
        var cut = RenderComponent<LoadingScreen>();

        Assert.Contains("Triangle", cut.Markup);
        Assert.Contains("Step Library", cut.Markup);
    }

    [Fact]
    public void LoadingScreen_RendersSpinner()
    {
        var cut = RenderComponent<LoadingScreen>();

        Assert.Contains("loading-spinner", cut.Markup);
    }
}
