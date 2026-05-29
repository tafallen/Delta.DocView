using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;
using Microsoft.AspNetCore.Components;

namespace Delta.DocView.Tests.Components;

public class ComposerRowTests : TestContext
{
    private static ComposerItem MakeItem(string id = "s1", string type = "Given") =>
        ComposerItem.From(new Step
        {
            Id = id, Type = type, Pattern = $"pattern {id}",
            Domain = "Auth", File = "T.cs", Line = 1
        });

    [Fact]
    public void ComposerRow_RendersKeyword()
    {
        var cut = RenderComponent<ComposerRow>(p => p
            .Add(c => c.Item, MakeItem())
            .Add(c => c.Keyword, "Given")
            .Add(c => c.OnRemove, EventCallback.Empty)
            .Add(c => c.OnNavigate, EventCallback.Empty));

        Assert.Contains("Given", cut.Markup);
    }

    [Fact]
    public void ComposerRow_RendersPattern()
    {
        var cut = RenderComponent<ComposerRow>(p => p
            .Add(c => c.Item, MakeItem("s1"))
            .Add(c => c.Keyword, "Given")
            .Add(c => c.OnRemove, EventCallback.Empty)
            .Add(c => c.OnNavigate, EventCallback.Empty));

        Assert.Contains("pattern s1", cut.Markup);
    }

    [Fact]
    public void ComposerRow_RemoveButtonClick_InvokesOnRemove()
    {
        var removed = false;
        var cut = RenderComponent<ComposerRow>(p => p
            .Add(c => c.Item, MakeItem())
            .Add(c => c.Keyword, "Given")
            .Add(c => c.OnRemove, EventCallback.Factory.Create(this, () => removed = true))
            .Add(c => c.OnNavigate, EventCallback.Empty));

        cut.Find("[data-testid='row-remove']").Click();
        Assert.True(removed);
    }

    [Fact]
    public void ComposerRow_NavigateButtonClick_InvokesOnNavigate()
    {
        var navigated = false;
        var cut = RenderComponent<ComposerRow>(p => p
            .Add(c => c.Item, MakeItem())
            .Add(c => c.Keyword, "Given")
            .Add(c => c.OnRemove, EventCallback.Empty)
            .Add(c => c.OnNavigate, EventCallback.Factory.Create(this, () => navigated = true)));

        cut.Find("[data-testid='row-navigate']").Click();
        Assert.True(navigated);
    }

    [Fact]
    public void ComposerRow_IsDragging_AppliesClass()
    {
        var cut = RenderComponent<ComposerRow>(p => p
            .Add(c => c.Item, MakeItem())
            .Add(c => c.Keyword, "Given")
            .Add(c => c.IsDragging, true)
            .Add(c => c.OnRemove, EventCallback.Empty)
            .Add(c => c.OnNavigate, EventCallback.Empty));

        Assert.Contains("is-drag", cut.Markup);
    }

    [Fact]
    public void Renders_Value_Input_Per_Param()
    {
        var step = new Step
        {
            Id = "s-param", Type = "Given",
            Pattern = "I am {actor : string}",
            Domain = "Auth", File = "T.cs", Line = 1,
            Params = [new StepParam { Name = "actor", Type = "string", Example = "admin" }]
        };
        var item = new ComposerItem(Guid.NewGuid(), step, ["admin"]);

        var cut = RenderComponent<ComposerRow>(p => p
            .Add(c => c.Item, item)
            .Add(c => c.Keyword, "Given")
            .Add(c => c.OnRemove, EventCallback.Empty)
            .Add(c => c.OnNavigate, EventCallback.Empty));

        var inputs = cut.FindAll("[data-testid='param-value-input']");
        Assert.Single(inputs);
        Assert.Equal("admin", inputs[0].GetAttribute("value"));
    }

    [Fact]
    public void No_Params_Renders_No_Inputs()
    {
        var cut = RenderComponent<ComposerRow>(p => p
            .Add(c => c.Item, MakeItem())
            .Add(c => c.Keyword, "Given")
            .Add(c => c.OnRemove, EventCallback.Empty)
            .Add(c => c.OnNavigate, EventCallback.Empty));

        Assert.Empty(cut.FindAll("[data-testid='param-value-input']"));
    }

    [Fact]
    public void Input_Change_Fires_OnParamValueChanged()
    {
        var step = new Step
        {
            Id = "s-cb", Type = "Given",
            Pattern = "I am {actor : string}",
            Domain = "Auth", File = "T.cs", Line = 1,
            Params = [new StepParam { Name = "actor", Type = "string", Example = "admin" }]
        };
        var item = new ComposerItem(Guid.NewGuid(), step, ["admin"]);

        (int idx, string val) captured = (-1, "");
        var cut = RenderComponent<ComposerRow>(p => p
            .Add(c => c.Item, item)
            .Add(c => c.Keyword, "Given")
            .Add(c => c.OnRemove, EventCallback.Empty)
            .Add(c => c.OnNavigate, EventCallback.Empty)
            .Add(c => c.OnParamValueChanged, (v) => captured = v));

        cut.Find("[data-testid='param-value-input']").Input("hello");

        Assert.Equal((0, "hello"), captured);
    }
}
