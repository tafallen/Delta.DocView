using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Delta.DocView.Tests.Components;

public class ScenarioComposerTests : TestContext
{
    private static Step MakeStep(string id = "s1", string type = "Given") => new()
    {
        Id = id, Type = type, Pattern = $"pattern {id}", Domain = "Auth",
        File = "T.cs", Line = 1
    };

    private ComposerState MakeComposer()
    {
        Services.AddScoped<ClientStepLibraryStore>();
        Services.AddScoped<FilterState>();
        Services.AddScoped<IFavouritesStore, InMemoryFavouritesStore>();
        Services.AddScoped<FilteredStepsProvider>();
        Services.AddScoped<IKeyboardActions, KeyboardActions>();
        Services.AddScoped<SelectionState>();
        Services.AddScoped<ComposerState>();
        JSInterop.Mode = JSRuntimeMode.Loose;
        return Services.GetRequiredService<ComposerState>();
    }

    [Fact]
    public void ScenarioComposer_RendersTabBar()
    {
        MakeComposer();
        var cut = RenderComponent<ScenarioComposer>();
        Assert.NotNull(cut.Find("[data-testid='composer-tab']"));
        Assert.Contains("Scenario Composer", cut.Markup);
    }

    [Fact]
    public void ScenarioComposer_TabClick_ShowsBody()
    {
        MakeComposer();
        var cut = RenderComponent<ScenarioComposer>();
        cut.Find("[data-testid='composer-tab']").Click();
        Assert.Contains("composer-body", cut.Markup);
    }

    [Fact]
    public void ScenarioComposer_WhenOpen_EmptyState_ShowsHelpText()
    {
        var composer = MakeComposer();
        composer.Toggle();
        var cut = RenderComponent<ScenarioComposer>();
        Assert.Contains("composer-empty", cut.Markup);
    }

    [Fact]
    public void ScenarioComposer_WithSteps_ShowsCountBadge()
    {
        var composer = MakeComposer();
        composer.AddStep(MakeStep());
        var cut = RenderComponent<ScenarioComposer>();
        Assert.Contains("composer-tab-count", cut.Markup);
        Assert.Contains("1", cut.Markup);
    }

    [Fact]
    public void ScenarioComposer_WhenOpen_WithSteps_ShowsFeaturePreview()
    {
        var composer = MakeComposer();
        composer.AddStep(MakeStep("s1", "Given"));
        composer.SetScenarioName("My Test");
        var cut = RenderComponent<ScenarioComposer>();
        Assert.Contains("composer-feature-preview", cut.Markup);
        Assert.Contains("Given pattern s1", cut.Markup);
    }

    [Fact]
    public void ScenarioComposer_ClearButton_ShowsConfirmDialog()
    {
        var composer = MakeComposer();
        composer.AddStep(MakeStep());
        var cut = RenderComponent<ScenarioComposer>();
        cut.Find("[data-testid='clear-composer']").Click();
        Assert.NotNull(cut.Find("[data-testid='confirm-clear']"));
    }

    [Fact]
    public void ScenarioComposer_ConfirmClear_EmptiesSteps()
    {
        var composer = MakeComposer();
        composer.AddStep(MakeStep());
        var cut = RenderComponent<ScenarioComposer>();
        cut.Find("[data-testid='clear-composer']").Click();
        cut.Find("[data-testid='confirm-clear']").Click();
        Assert.Empty(composer.Steps);
    }

    [Fact]
    public void ScenarioComposer_CancelClear_KeepsSteps()
    {
        var composer = MakeComposer();
        composer.AddStep(MakeStep());
        var cut = RenderComponent<ScenarioComposer>();
        cut.Find("[data-testid='clear-composer']").Click();
        cut.Find("[data-testid='cancel-clear']").Click();
        Assert.Single(composer.Steps);
    }

    [Fact]
    public void ScenarioComposer_ConsecutiveSameType_ShowsAndKeyword()
    {
        var composer = MakeComposer();
        composer.AddStep(new Step { Id = "s1", Type = "Given", Pattern = "I am logged in", Domain = "Auth", File = "T.cs", Line = 1 });
        composer.AddStep(new Step { Id = "s2", Type = "Given", Pattern = "I have a token", Domain = "Auth", File = "T.cs", Line = 2 });
        // AddStep opens composer automatically
        var cut = RenderComponent<ScenarioComposer>();
        Assert.Contains("And", cut.Markup);
    }

    [Fact]
    public void ScenarioComposer_SaveButton_Disabled_When_Empty()
    {
        MakeComposer();
        var cut = RenderComponent<ScenarioComposer>();
        cut.Find("[data-testid='composer-tab']").Click();
        Assert.True(cut.Find("[data-testid='save-composer']").HasAttribute("disabled"));
    }

    [Fact]
    public void ScenarioComposer_SaveButton_Enabled_With_Steps()
    {
        var composer = MakeComposer();
        composer.AddStep(MakeStep());
        var cut = RenderComponent<ScenarioComposer>();
        Assert.False(cut.Find("[data-testid='save-composer']").HasAttribute("disabled"));
    }

    [Fact]
    public void ScenarioComposer_SaveClick_Invokes_SaveTextFile_With_Filename_And_Content()
    {
        var composer = MakeComposer();
        composer.SetScenarioName("My Login Flow");
        composer.AddStep(MakeStep());
        var planned = JSInterop.SetupVoid("docview.saveTextFile", _ => true).SetVoidResult();

        var cut = RenderComponent<ScenarioComposer>();
        cut.Find("[data-testid='save-composer']").Click();

        var call = JSInterop.Invocations["docview.saveTextFile"].Single();
        Assert.Equal("my-login-flow.feature", call.Arguments[0]);
        Assert.Equal(composer.FeatureText, call.Arguments[1]);
    }
}
