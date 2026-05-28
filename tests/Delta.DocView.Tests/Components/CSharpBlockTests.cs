using Bunit;
using Delta.DocView.Client.Components;

namespace Delta.DocView.Tests.Components;

public class CSharpBlockTests : TestContext
{
    [Fact]
    public void Collapsed_By_Default()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<CSharpBlock>(p => p.Add(c => c.Source, "public void Foo()"));

        Assert.Empty(cut.FindAll("[data-testid='cs-source']"));
        Assert.Contains("▸ Show", cut.Find("[data-testid='cs-toggle']").TextContent);
    }

    [Fact]
    public void Clicking_Toggle_Expands()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<CSharpBlock>(p => p.Add(c => c.Source, "public void Foo()"));

        cut.Find("[data-testid='cs-toggle']").Click();

        Assert.NotEmpty(cut.FindAll("[data-testid='cs-source']"));
        Assert.Contains("▾ Hide", cut.Find("[data-testid='cs-toggle']").TextContent);
    }

    [Fact]
    public void Clicking_Toggle_Twice_Collapses()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<CSharpBlock>(p => p.Add(c => c.Source, "public void Foo()"));

        cut.Find("[data-testid='cs-toggle']").Click();
        cut.Find("[data-testid='cs-toggle']").Click();

        Assert.Empty(cut.FindAll("[data-testid='cs-source']"));
        Assert.Contains("▸ Show", cut.Find("[data-testid='cs-toggle']").TextContent);
    }

    [Fact]
    public void Expanded_Shows_Copy_Button()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<CSharpBlock>(p => p.Add(c => c.Source, "public void Foo()"));

        cut.Find("[data-testid='cs-toggle']").Click();

        Assert.NotEmpty(cut.FindAll("[data-testid='copy-button']"));
    }

    [Fact]
    public void Copy_Click_Invokes_JsCopyText()
    {
        JSInterop.Setup<bool>("docview.copyText", _ => true).SetResult(true);

        var cut = RenderComponent<CSharpBlock>(p => p.Add(c => c.Source, "public void Foo()"));

        cut.Find("[data-testid='cs-toggle']").Click();
        cut.Find("[data-testid='copy-button']").Click();

        var calls = JSInterop.Invocations["docview.copyText"];
        Assert.Single(calls);
        Assert.Equal("public void Foo()", calls[0].Arguments[0]);
    }

    [Fact]
    public void Copy_Confirmation_Appears_Then_Disappears()
    {
        JSInterop.Setup<bool>("docview.copyText", _ => true).SetResult(true);

        var cut = RenderComponent<CSharpBlock>(p => p.Add(c => c.Source, "public void Foo()"));

        cut.Find("[data-testid='cs-toggle']").Click();
        cut.Find("[data-testid='copy-button']").Click();

        Assert.NotEmpty(cut.FindAll("[data-testid='copy-confirmation']"));

        cut.WaitForAssertion(
            () => Assert.Empty(cut.FindAll("[data-testid='copy-confirmation']")),
            TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Source_Renders_Highlighted_Tokens()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<CSharpBlock>(p => p.Add(c => c.Source, "public void Foo()"));

        cut.Find("[data-testid='cs-toggle']").Click();

        var html = cut.Find("[data-testid='cs-source']").InnerHtml;
        Assert.Contains("<span class=\"cs-kw\">public</span>", html);
        Assert.Contains("<span class=\"cs-kw\">void</span>", html);
    }
}
