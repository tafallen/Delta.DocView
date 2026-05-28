using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;

namespace Delta.DocView.Tests.Services;

public class SelectionStateTests
{
    private static Step MakeStep(string id = "s1") => new() { Id = id, Type = "Given", Pattern = "p" };

    [Fact]
    public void Default_SelectedIsNull_AndNoSubscriberRequired()
    {
        var state = new SelectionState();
        Assert.Null(state.Selected);
    }

    [Fact]
    public void Select_Step_UpdatesSelectedAndRaisesChangedOnce()
    {
        var state = new SelectionState();
        var raised = 0;
        state.Changed += () => raised++;
        var step = MakeStep();

        state.Select(step);

        Assert.Same(step, state.Selected);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Select_SameStep_IsNoOp()
    {
        var state = new SelectionState();
        var step = MakeStep();
        state.Select(step);
        var raised = 0;
        state.Changed += () => raised++;

        state.Select(step);

        Assert.Same(step, state.Selected);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void Select_Null_AfterSelection_ClearsAndRaises()
    {
        var state = new SelectionState();
        state.Select(MakeStep());
        var raised = 0;
        state.Changed += () => raised++;

        state.Select(null);

        Assert.Null(state.Selected);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Select_Null_WhenAlreadyNull_IsNoOp()
    {
        var state = new SelectionState();
        var raised = 0;
        state.Changed += () => raised++;

        state.Select(null);

        Assert.Null(state.Selected);
        Assert.Equal(0, raised);
    }
}
