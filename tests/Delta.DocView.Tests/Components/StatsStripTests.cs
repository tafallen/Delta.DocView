using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Shared.Models;

namespace Delta.DocView.Tests.Components;

public class StatsStripTests : TestContext
{
    [Fact]
    public void Renders_Used_Count()
    {
        var step = new Step { Used = 7, File = "File.cs", Line = 42 };

        var cut = RenderComponent<StatsStrip>(p => p.Add(c => c.Step, step));

        var used = cut.Find("[data-testid='stat-used']");
        Assert.Contains("7 scenarios", used.TextContent);
    }

    [Fact]
    public void Renders_Source_File_And_Line()
    {
        var step = new Step { File = "File.cs", Line = 42 };

        var cut = RenderComponent<StatsStrip>(p => p.Add(c => c.Step, step));

        var source = cut.Find("[data-testid='stat-source']");
        Assert.Contains("File.cs:42", source.TextContent);
    }

    [Fact]
    public void Renders_Tags_When_Present()
    {
        var step = new Step
        {
            File = "File.cs",
            Line = 1,
            Tags = new[] { "login", "api" },
        };

        var cut = RenderComponent<StatsStrip>(p => p.Add(c => c.Step, step));

        var tags = cut.Find("[data-testid='stat-tags']");
        Assert.Contains("login · api", tags.TextContent);
    }

    [Fact]
    public void Hides_Tags_When_Empty()
    {
        var step = new Step { File = "File.cs", Line = 1, Tags = Array.Empty<string>() };

        var cut = RenderComponent<StatsStrip>(p => p.Add(c => c.Step, step));

        Assert.Empty(cut.FindAll("[data-testid='stat-tags']"));
    }
}
