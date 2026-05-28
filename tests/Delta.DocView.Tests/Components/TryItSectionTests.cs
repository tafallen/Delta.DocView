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
    public void Inputs_Are_Paired_With_Labels_Via_For_Id()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<TryItSection>(p => p.Add(c => c.Step, MakeLoggedInStep()));

        var inputs = cut.FindAll("input");
        Assert.NotEmpty(inputs);
        foreach (var input in inputs)
        {
            var id = input.GetAttribute("id");
            Assert.False(string.IsNullOrEmpty(id), "input must have id");
            var label = cut.Find($"label[for='{id}']");
            Assert.NotNull(label);
        }
    }

    [Fact]
    public void Mismatch_Warning_Hidden_When_Counts_Match()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var step = new Step
        {
            Id = "logged-in",
            Type = "Given",
            Pattern = "as {username : string}",
            Params = new[]
            {
                new StepParam { Name = "username", Type = "string", Example = "\"admin\"" }
            }
        };

        var cut = RenderComponent<TryItSection>(p => p.Add(c => c.Step, step));

        Assert.Empty(cut.FindAll("[data-testid='try-it-mismatch']"));
    }

    [Fact]
    public void Mismatch_Warning_Shown_When_Pattern_Has_More_Tokens_Than_Params()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var step = new Step
        {
            Id = "two-tokens",
            Type = "Given",
            Pattern = "as {a : string} and {b : string}",
            Params = new[]
            {
                new StepParam { Name = "a", Type = "string", Example = "\"x\"" }
            }
        };

        var cut = RenderComponent<TryItSection>(p => p.Add(c => c.Step, step));

        var banner = cut.Find("[data-testid='try-it-mismatch']");
        Assert.NotNull(banner);
        Assert.Contains("2 parameter tokens", banner.TextContent);
        Assert.Contains("1 param", banner.TextContent);
    }

    [Fact]
    public void Mismatch_Warning_Shown_When_Pattern_Has_Fewer_Tokens_Than_Params()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var step = new Step
        {
            Id = "no-tokens",
            Type = "Given",
            Pattern = "the system is ready",
            Params = new[]
            {
                new StepParam { Name = "extra", Type = "string", Example = "\"x\"" }
            }
        };

        var cut = RenderComponent<TryItSection>(p => p.Add(c => c.Step, step));

        var banner = cut.Find("[data-testid='try-it-mismatch']");
        Assert.NotNull(banner);
        Assert.Contains("0 parameter tokens", banner.TextContent);
        Assert.Contains("1 param", banner.TextContent);
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
