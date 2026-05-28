using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Shared.Models;

namespace Delta.DocView.Tests.Components;

public class PatternHeadingTests : TestContext
{
    [Fact]
    public void Renders_H2_With_DetailPattern_Class()
    {
        var step = new Step { Pattern = "I am logged in as {username : string}" };

        var cut = RenderComponent<PatternHeading>(p => p.Add(c => c.Step, step));

        var h2 = cut.Find("h2");
        Assert.Contains("detail-pattern", h2.ClassList);
    }

    [Fact]
    public void Renders_PatternRenderer_Output_With_Pill_For_Typed_Token()
    {
        var step = new Step { Pattern = "I am logged in as {username : string}" };

        var cut = RenderComponent<PatternHeading>(p => p.Add(c => c.Step, step));

        Assert.NotEmpty(cut.FindAll(".param-pill"));
    }
}
