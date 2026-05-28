using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;

namespace Delta.DocView.Tests.Services;

public class ClientStepLibraryStoreTests
{
    private static StepLibrary MakeLibrary(string version, string generatedAt, int stepCount = 0)
    {
        var steps = Enumerable.Range(0, stepCount)
            .Select(i => new Step { Id = $"s{i}", Type = "Given", Pattern = $"pattern {i}", Domain = "Auth" })
            .ToList<Step>();

        return new StepLibrary
        {
            Version = version,
            GeneratedAt = generatedAt,
            Domains = [],
            Steps = steps,
            Signature = new StepSignature { Algorithm = "SHA-256", Digest = new string('0', 64) }
        };
    }

    [Fact]
    public void Populate_SetsVersion()
    {
        var store = new ClientStepLibraryStore();
        store.Populate(MakeLibrary("2.5.0", "2026-01-15T00:00:00Z"));
        Assert.Equal("2.5.0", store.Version);
    }

    [Fact]
    public void Populate_SetsGeneratedAt()
    {
        var store = new ClientStepLibraryStore();
        store.Populate(MakeLibrary("1.0.0", "2026-03-01T12:00:00Z"));
        Assert.Equal("2026-03-01T12:00:00Z", store.GeneratedAt);
    }

    [Fact]
    public void Populate_SetsStepsAndById()
    {
        var store = new ClientStepLibraryStore();
        store.Populate(MakeLibrary("1.0.0", "2026-01-01T00:00:00Z", stepCount: 3));
        Assert.Equal(3, store.Steps.Count);
        Assert.Equal(3, store.ById.Count);
        Assert.True(store.ById.ContainsKey("s0"));
        Assert.True(store.ById.ContainsKey("s2"));
    }
}
