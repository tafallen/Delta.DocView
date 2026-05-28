using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;

namespace Delta.DocView.Tests.Services;

public class StepRankingTests
{
    private static Step MakeStep(string id, string pattern, int used) => new()
    {
        Id = id,
        Type = "Given",
        Pattern = pattern,
        Domain = "Auth",
        Params = [],
        Used = used,
    };

    [Fact]
    public void Rank_OrdersByUsageDescending()
    {
        var input = new[]
        {
            MakeStep("A", "alpha", 10),
            MakeStep("B", "bravo", 50),
            MakeStep("C", "charlie", 30),
        };

        var result = StepRanking.Rank(input);

        Assert.Equal(new[] { "B", "C", "A" }, result.Select(s => s.Id));
    }

    [Fact]
    public void Rank_TieBreaksByPatternAscendingOrdinal()
    {
        var input = new[]
        {
            MakeStep("ban", "banana", 5),
            MakeStep("app", "apple", 5),
        };

        var result = StepRanking.Rank(input);

        Assert.Equal(new[] { "app", "ban" }, result.Select(s => s.Id));
    }

    [Fact]
    public void Rank_PreservesInputCountAndItems()
    {
        var input = new[]
        {
            MakeStep("s1", "one", 1),
            MakeStep("s2", "two", 2),
            MakeStep("s3", "three", 3),
            MakeStep("s4", "four", 4),
        };

        var result = StepRanking.Rank(input);

        Assert.Equal(input.Length, result.Count);
        Assert.All(result, s => Assert.NotNull(s));
        Assert.Equal(input.Select(s => s.Id).OrderBy(x => x), result.Select(s => s.Id).OrderBy(x => x));
    }

    [Fact]
    public void Rank_EmptyInput_ReturnsEmpty()
    {
        var result = StepRanking.Rank(Array.Empty<Step>());

        Assert.Empty(result);
    }

    [Fact]
    public void Rank_StableForEqualUsedAndPattern()
    {
        var a = MakeStep("dup1", "same", 7);
        var b = MakeStep("dup2", "same", 7);

        var result = StepRanking.Rank(new[] { a, b });

        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.Id == "dup1");
        Assert.Contains(result, s => s.Id == "dup2");
    }
}
