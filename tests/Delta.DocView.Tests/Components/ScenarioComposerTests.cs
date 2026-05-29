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
    public void ClearDialog_Escape_Cancels()
    {
        var composer = MakeComposer();
        composer.AddStep(MakeStep());
        var cut = RenderComponent<ScenarioComposer>();
        cut.Find("[data-testid='clear-composer']").Click();

        cut.Find("[role='dialog']").KeyDown(key: "Escape");

        Assert.Empty(cut.FindAll("[data-testid='confirm-clear']"));
        Assert.Single(composer.Steps);
    }

    [Fact]
    public void ClearDialog_Has_Dialog_Role_And_AriaModal()
    {
        var composer = MakeComposer();
        composer.AddStep(MakeStep());
        var cut = RenderComponent<ScenarioComposer>();
        cut.Find("[data-testid='clear-composer']").Click();

        var dialog = cut.Find(".composer-clear-dialog");
        Assert.Equal("dialog", dialog.GetAttribute("role"));
        Assert.Equal("true", dialog.GetAttribute("aria-modal"));
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
    public void Drag_Drop_Reorders_Steps()
    {
        var composer = MakeComposer();
        composer.AddStep(MakeStep("A"));
        composer.AddStep(MakeStep("B"));
        composer.AddStep(MakeStep("C"));
        var cut = RenderComponent<ScenarioComposer>();

        var rows = cut.FindAll(".composer-row");
        // Drag the first row (A) and drop it onto the third row (C).
        rows[0].QuerySelector(".drag-handle")!.DragStart();
        cut.FindAll(".composer-row")[2].Drop();

        // MoveStep(A, indexOf(C)=2) => B, C, A
        Assert.Equal(
            new[] { "B", "C", "A" },
            composer.Steps.Select(s => s.Step.Id).ToArray());
    }

    [Fact]
    public void Drag_Drop_Onto_Self_Is_NoOp()
    {
        var composer = MakeComposer();
        composer.AddStep(MakeStep("A"));
        composer.AddStep(MakeStep("B"));
        composer.AddStep(MakeStep("C"));
        var cut = RenderComponent<ScenarioComposer>();

        var rows = cut.FindAll(".composer-row");
        // Drag row B and drop it on itself.
        rows[1].QuerySelector(".drag-handle")!.DragStart();
        cut.FindAll(".composer-row")[1].Drop();

        Assert.Equal(
            new[] { "A", "B", "C" },
            composer.Steps.Select(s => s.Step.Id).ToArray());
    }

    [Fact]
    public void Drop_Without_DragStart_Is_NoOp()
    {
        var composer = MakeComposer();
        composer.AddStep(MakeStep("A"));
        composer.AddStep(MakeStep("B"));
        var cut = RenderComponent<ScenarioComposer>();

        // Drop without a preceding drag-start (_draggingId is null).
        cut.FindAll(".composer-row")[1].Drop();

        Assert.Equal(
            new[] { "A", "B" },
            composer.Steps.Select(s => s.Step.Id).ToArray());
    }

    [Fact]
    public void MoveDown_Button_Reorders_Step()
    {
        var composer = MakeComposer();
        composer.AddStep(MakeStep("A"));
        composer.AddStep(MakeStep("B"));
        composer.AddStep(MakeStep("C"));
        var cut = RenderComponent<ScenarioComposer>();

        cut.FindAll(".composer-row")[0].QuerySelector("[data-testid='move-down']")!.Click();

        Assert.Equal(
            new[] { "B", "A", "C" },
            composer.Steps.Select(s => s.Step.Id).ToArray());
    }

    [Fact]
    public void MoveUp_Button_Reorders_Step()
    {
        var composer = MakeComposer();
        composer.AddStep(MakeStep("A"));
        composer.AddStep(MakeStep("B"));
        composer.AddStep(MakeStep("C"));
        var cut = RenderComponent<ScenarioComposer>();

        cut.FindAll(".composer-row")[2].QuerySelector("[data-testid='move-up']")!.Click();

        Assert.Equal(
            new[] { "A", "C", "B" },
            composer.Steps.Select(s => s.Step.Id).ToArray());
    }

    [Fact]
    public void MoveUp_Disabled_On_First_Row()
    {
        var composer = MakeComposer();
        composer.AddStep(MakeStep("A"));
        composer.AddStep(MakeStep("B"));
        var cut = RenderComponent<ScenarioComposer>();

        var btn = cut.FindAll(".composer-row")[0].QuerySelector("[data-testid='move-up']")!;
        Assert.True(btn.HasAttribute("disabled"));
    }

    [Fact]
    public void MoveDown_Disabled_On_Last_Row()
    {
        var composer = MakeComposer();
        composer.AddStep(MakeStep("A"));
        composer.AddStep(MakeStep("B"));
        var cut = RenderComponent<ScenarioComposer>();

        var rows = cut.FindAll(".composer-row");
        var btn = rows[rows.Count - 1].QuerySelector("[data-testid='move-down']")!;
        Assert.True(btn.HasAttribute("disabled"));
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
