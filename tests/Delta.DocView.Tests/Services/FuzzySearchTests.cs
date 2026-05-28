using Delta.DocView.Client.Services;

namespace Delta.DocView.Tests.Services;

public class FuzzySearchTests
{
    [Fact]
    public void Empty_Needle_ReturnsZero()
    {
        Assert.Equal(0, FuzzySearch.Score("", "anything"));
    }

    [Fact]
    public void Empty_Hay_ReturnsZero()
    {
        Assert.Equal(0, FuzzySearch.Score("a", ""));
    }

    [Fact]
    public void Exact_Substring_Match_ScoresHigh()
    {
        Assert.True(FuzzySearch.Score("log", "login") > 50);
    }

    [Fact]
    public void Subsequence_Match_Scores_NonZero()
    {
        Assert.True(FuzzySearch.Score("log", "I am logged in") > 0);
    }

    [Fact]
    public void No_Match_ReturnsZero()
    {
        Assert.Equal(0, FuzzySearch.Score("xyz", "hello"));
    }

    [Fact]
    public void Partial_Match_ReturnsZero()
    {
        Assert.Equal(0, FuzzySearch.Score("xyz", "xy"));
    }

    [Fact]
    public void Case_Insensitive_Both_Sides()
    {
        Assert.True(FuzzySearch.Score("LOG", "logged") > 0);
        Assert.True(FuzzySearch.Score("log", "LOGGED") > 0);
    }

    [Fact]
    public void Word_Boundary_Bonus()
    {
        Assert.True(FuzzySearch.Score("u", "user") > FuzzySearch.Score("u", "shoulder"));
    }

    [Fact]
    public void Consecutive_Match_Bonus()
    {
        Assert.True(FuzzySearch.Score("log", "login system") > FuzzySearch.Score("log", "lXoXg"));
    }

    [Fact]
    public void Start_Of_String_Bonus()
    {
        Assert.True(FuzzySearch.Score("l", "login") > FuzzySearch.Score("l", "hello"));
    }

    [Fact]
    public void Order_Matters_ReversedFails()
    {
        Assert.True(FuzzySearch.Score("og", "logging") > 0);
        Assert.Equal(0, FuzzySearch.Score("go", "logging"));
    }
}
