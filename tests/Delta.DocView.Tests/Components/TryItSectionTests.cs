using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Shared.Models;

namespace Delta.DocView.Tests.Components;

public class TryItSectionTests : TestContext
{
    private static Step MakeLoggedInStep() => new()
    {
        Id = "logged-in",
        Type = "Given",
        Pattern = "I am logged in as {username : string}",
        Params = new[]
        {
            new StepParam { Name = "username", Type = "string", Example = "\"admin\"" }
        }
    };

    [Fact]
    public void Renders_One_Input_Per_ParamToken_Prefilled_With_Example()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<TryItSection>(p => p.Add(c => c.Step, MakeLoggedInStep()));

        var inputs = cut.FindAll("input");
        Assert.Single(inputs);
        Assert.Equal("\"admin\"", inputs[0].GetAttribute("value"));
    }

    [Fact]
    public void Composed_Line_Reflects_Param_Examples_By_Default()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<TryItSection>(p => p.Add(c => c.Step, MakeLoggedInStep()));

        var composed = cut.Find("[data-testid='composed-line']");
        Assert.Equal("I am logged in as \"admin\"", composed.TextContent);
    }

    [Fact]
    public void Typing_Into_Input_Updates_Composed_Line()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<TryItSection>(p => p.Add(c => c.Step, MakeLoggedInStep()));

        cut.Find("input").Input("\"root\"");

        var composed = cut.Find("[data-testid='composed-line']");
        Assert.Equal("I am logged in as \"root\"", composed.TextContent);
    }

    [Fact]
    public void Copy_Click_Invokes_JsCopyText_With_Composed_Line()
    {
        var invocation = JSInterop.Setup<bool>("docview.copyText", _ => true).SetResult(true);

        var cut = RenderComponent<TryItSection>(p => p.Add(c => c.Step, MakeLoggedInStep()));

        cut.Find("[data-testid='copy-button']").Click();

        var calls = JSInterop.Invocations["docview.copyText"];
        Assert.Single(calls);
        Assert.Equal("I am logged in as \"admin\"", calls[0].Arguments[0]);
    }

    [Fact]
    public void Copy_Shows_Confirmation_That_Disappears()
    {
        JSInterop.Setup<bool>("docview.copyText", _ => true).SetResult(true);

        var cut = RenderComponent<TryItSection>(p => p.Add(c => c.Step, MakeLoggedInStep()));

        cut.Find("[data-testid='copy-button']").Click();

        Assert.NotEmpty(cut.FindAll("[data-testid='copy-confirmation']"));

        cut.WaitForAssertion(
            () => Assert.Empty(cut.FindAll("[data-testid='copy-confirmation']")),
            TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void No_Params_Step_Skips_Inputs_And_Composes_From_Static_Text()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var step = new Step
        {
            Id = "ready",
            Type = "Given",
            Pattern = "the system is ready",
            Params = Array.Empty<StepParam>()
        };

        var cut = RenderComponent<TryItSection>(p => p.Add(c => c.Step, step));

        Assert.Empty(cut.FindAll("input"));
        Assert.Equal("the system is ready", cut.Find("[data-testid='composed-line']").TextContent);
        Assert.NotNull(cut.Find("[data-testid='copy-button']"));
    }
}
