using Bunit;
using Delta.DocView.Client.Components;

namespace Delta.DocView.Tests.Components;

public class CopyButtonTests : TestContext
{
    [Fact]
    public void Renders_Button_With_Label()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<CopyButton>(p => p
            .Add(c => c.Text, "anything")
            .Add(c => c.Label, "Copy thing"));

        Assert.Contains("Copy thing", cut.Find("[data-testid='copy-button']").TextContent);
    }

    [Fact]
    public void Click_Invokes_CopyText_With_Text_Parameter()
    {
        JSInterop.Setup<bool>("docview.copyText", _ => true).SetResult(true);

        var cut = RenderComponent<CopyButton>(p => p
            .Add(c => c.Text, "hello world"));

        cut.Find("[data-testid='copy-button']").Click();

        var calls = JSInterop.Invocations["docview.copyText"];
        Assert.Single(calls);
        Assert.Equal("hello world", calls[0].Arguments[0]);
    }

    [Fact]
    public void Click_Shows_Confirmation_Then_Disappears()
    {
        JSInterop.Setup<bool>("docview.copyText", _ => true).SetResult(true);

        var cut = RenderComponent<CopyButton>(p => p.Add(c => c.Text, "x"));

        cut.Find("[data-testid='copy-button']").Click();

        Assert.NotEmpty(cut.FindAll("[data-testid='copy-confirmation']"));

        cut.WaitForAssertion(
            () => Assert.Empty(cut.FindAll("[data-testid='copy-confirmation']")),
            TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Button_Has_Aria_Label_From_Label_Parameter()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<CopyButton>(p => p
            .Add(c => c.Text, "x")
            .Add(c => c.Label, "Copy code"));

        Assert.Equal("Copy code", cut.Find("[data-testid='copy-button']").GetAttribute("aria-label"));
    }

    [Fact]
    public void Confirmation_Label_Is_Configurable()
    {
        JSInterop.Setup<bool>("docview.copyText", _ => true).SetResult(true);

        var cut = RenderComponent<CopyButton>(p => p
            .Add(c => c.Text, "x")
            .Add(c => c.ConfirmLabel, "✓ done"));

        cut.Find("[data-testid='copy-button']").Click();

        Assert.Equal("✓ done", cut.Find("[data-testid='copy-confirmation']").TextContent);
    }
}
