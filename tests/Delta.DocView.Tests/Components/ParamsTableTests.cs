using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Shared.Models;

namespace Delta.DocView.Tests.Components;

public class ParamsTableTests : TestContext
{
    [Fact]
    public void Hidden_When_No_Params()
    {
        var step = new Step { Params = Array.Empty<StepParam>() };

        var cut = RenderComponent<ParamsTable>(p => p.Add(c => c.Step, step));

        Assert.Empty(cut.FindAll("[data-testid='params-table']"));
    }

    [Fact]
    public void Renders_Row_Per_Param()
    {
        var step = new Step
        {
            Params = new[]
            {
                new StepParam { Name = "a", Type = "string", Example = "x" },
                new StepParam { Name = "b", Type = "int", Example = "1" },
                new StepParam { Name = "c", Type = "bool", Example = "true" },
            },
        };

        var cut = RenderComponent<ParamsTable>(p => p.Add(c => c.Step, step));

        var rows = cut.FindAll(".params-table tbody tr");
        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public void Type_Cell_Has_Class_For_Param_Type()
    {
        var step = new Step
        {
            Params = new[]
            {
                new StepParam { Name = "a", Type = "string", Example = "x" },
            },
        };

        var cut = RenderComponent<ParamsTable>(p => p.Add(c => c.Step, step));

        var chip = cut.Find(".param-cell-type .param-type-chip");
        Assert.Contains("param-type-chip", chip.ClassList);
        Assert.Contains("param-string", chip.ClassList);
    }

    [Fact]
    public void Example_Cell_Renders_Verbatim()
    {
        var step = new Step
        {
            Params = new[]
            {
                new StepParam { Name = "u", Type = "string", Example = "\"admin\"" },
            },
        };

        var cut = RenderComponent<ParamsTable>(p => p.Add(c => c.Step, step));

        var exampleCode = cut.Find(".param-cell-example code");
        Assert.Equal("\"admin\"", exampleCode.TextContent);
    }
}
