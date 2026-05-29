using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Client.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Delta.DocView.Tests.Components;

public class CSharpBlockTests : TestContext
{
    public CSharpBlockTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddScoped<TweaksStore>();
    }

    [Fact]
    public void Collapsed_By_Default()
    {
        var cut = RenderComponent<CSharpBlock>(p => p.Add(c => c.Source, "public void Foo()"));

        Assert.Empty(cut.FindAll("[data-testid='cs-source']"));
        var toggleText = cut.Find("[data-testid='cs-toggle']").TextContent;
        Assert.Contains("▸", toggleText);
        Assert.Contains("C# step definition", toggleText);
    }

    [Fact]
    public void Expanded_When_SourceDefault_Is_Expanded()
    {
        var tweaks = Services.GetRequiredService<TweaksStore>();
        tweaks.SetSourceDefault(SourceDefaultOption.Expanded);

        var cut = RenderComponent<CSharpBlock>(p => p.Add(c => c.Source, "public void Foo()"));

        Assert.NotEmpty(cut.FindAll("[data-testid='cs-source']"));
    }

    [Fact]
    public void Clicking_Toggle_Expands()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<CSharpBlock>(p => p.Add(c => c.Source, "public void Foo()"));

        cut.Find("[data-testid='cs-toggle']").Click();

        Assert.NotEmpty(cut.FindAll("[data-testid='cs-source']"));
        var toggleText = cut.Find("[data-testid='cs-toggle']").TextContent;
        Assert.Contains("▾", toggleText);
        Assert.Contains("C# step definition", toggleText);
    }

    [Fact]
    public void Clicking_Toggle_Twice_Collapses()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<CSharpBlock>(p => p.Add(c => c.Source, "public void Foo()"));

        cut.Find("[data-testid='cs-toggle']").Click();
        cut.Find("[data-testid='cs-toggle']").Click();

        Assert.Empty(cut.FindAll("[data-testid='cs-source']"));
        var toggleText = cut.Find("[data-testid='cs-toggle']").TextContent;
        Assert.Contains("▸", toggleText);
        Assert.Contains("C# step definition", toggleText);
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
    public void Toggle_Has_Aria_Expanded_State()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<CSharpBlock>(p => p.Add(c => c.Source, "public void Foo()"));

        Assert.Equal("false", cut.Find("[data-testid='cs-toggle']").GetAttribute("aria-expanded"));

        cut.Find("[data-testid='cs-toggle']").Click();

        Assert.Equal("true", cut.Find("[data-testid='cs-toggle']").GetAttribute("aria-expanded"));
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
