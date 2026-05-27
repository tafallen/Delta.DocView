using Bunit;
using Delta.DocView.Client.Components;

namespace Delta.DocView.Tests.Components;

public class StartupErrorPageTests : TestContext
{
    [Fact]
    public void StartupErrorPage_ShowsProvidedErrorMessage()
    {
        var cut = RenderComponent<StartupErrorPage>(p =>
            p.Add(c => c.ErrorMessage, "Library file not found at '/data/step-library.json'."));

        Assert.Contains("Library file not found", cut.Markup);
    }

    [Fact]
    public void StartupErrorPage_ShowsDocviewLibraryPathHint()
    {
        var cut = RenderComponent<StartupErrorPage>(p =>
            p.Add(c => c.ErrorMessage, "some error"));

        Assert.Contains("DOCVIEW_LIBRARY_PATH", cut.Markup);
    }
}
