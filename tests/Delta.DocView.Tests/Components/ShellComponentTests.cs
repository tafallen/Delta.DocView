using Bunit;
using Delta.DocView.Client.Components;

namespace Delta.DocView.Tests.Components;

public class ShellComponentTests : TestContext
{
    [Fact]
    public void LeftRail_Renders()
    {
        var cut = RenderComponent<LeftRail>();
        Assert.Contains("left-rail-shell", cut.Markup);
    }

    [Fact]
    public void StepList_Renders()
    {
        var cut = RenderComponent<StepList>();
        Assert.Contains("step-list-shell", cut.Markup);
    }

    [Fact]
    public void DetailPanel_Renders()
    {
        var cut = RenderComponent<DetailPanel>();
        Assert.Contains("detail-panel-shell", cut.Markup);
    }
}
