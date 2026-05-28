using Delta.DocView.Client.Services;

namespace Delta.DocView.Tests.Services;

public class FilterStateTests
{
    [Fact]
    public void Default_SelectsAllFourTypes()
    {
        var state = new FilterState();
        Assert.Equal(4, state.Types.Count);
        Assert.Contains("Given", state.Types);
        Assert.Contains("When", state.Types);
        Assert.Contains("Then", state.Types);
        Assert.Contains("And", state.Types);
    }

    [Fact]
    public void ToggleType_RemovingLastSelected_IsNoOp()
    {
        var state = new FilterState();
        state.ToggleType("Given");
        state.ToggleType("When");
        state.ToggleType("Then");
        var raised = 0;
        state.Changed += () => raised++;

        state.ToggleType("And");

        Assert.Single(state.Types);
        Assert.Contains("And", state.Types);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void ToggleType_OnOff_RaisesOnChangedOncePerRealChange()
    {
        var state = new FilterState();
        var raised = 0;
        state.Changed += () => raised++;

        state.ToggleType("Given");
        Assert.Equal(1, raised);
        Assert.DoesNotContain("Given", state.Types);

        state.ToggleType("Given");
        Assert.Equal(2, raised);
        Assert.Contains("Given", state.Types);
    }

    [Fact]
    public void SetDomain_SameValue_DoesNotRaise()
    {
        var state = new FilterState();
        var raised = 0;
        state.Changed += () => raised++;

        state.SetDomain(null);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void ToggleParamType_AddThenRemove_RaisesChangedOnBoth()
    {
        var state = new FilterState();
        var raised = 0;
        state.Changed += () => raised++;

        state.ToggleParamType("string");
        Assert.Contains("string", state.ParamTypes);
        Assert.Equal(1, raised);

        state.ToggleParamType("string");
        Assert.DoesNotContain("string", state.ParamTypes);
        Assert.Equal(2, raised);
    }

    [Fact]
    public void SetQuery_Null_StoresEmptyString()
    {
        var state = new FilterState();
        state.SetQuery(null);
        Assert.Equal("", state.Query);
    }
}
