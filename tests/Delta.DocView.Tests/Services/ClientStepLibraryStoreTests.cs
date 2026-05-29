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

    private static StepLibrary MakeLibraryWith(IReadOnlyList<StepDomain> domains, IReadOnlyList<Step> steps) =>
        new()
        {
            Version = "1.0.0",
            GeneratedAt = "2026-01-01T00:00:00Z",
            Domains = domains,
            Steps = steps,
            Signature = new StepSignature { Algorithm = "SHA-256", Digest = new string('0', 64) }
        };

    [Fact]
    public void Populate_SetsCountByType_IncludingZeroForUnusedTypes()
    {
        var steps = new List<Step>
        {
            new() { Id = "s1", Type = "Given", Pattern = "p1", Domain = "Auth" },
            new() { Id = "s2", Type = "Given", Pattern = "p2", Domain = "Auth" },
            new() { Id = "s3", Type = "Then",  Pattern = "p3", Domain = "Auth" },
        };
        var store = new ClientStepLibraryStore();
        store.Populate(MakeLibraryWith([], steps));

        Assert.Equal(2, store.CountByType["Given"]);
        Assert.Equal(0, store.CountByType["When"]);
        Assert.Equal(1, store.CountByType["Then"]);
        Assert.Equal(3, store.CountByType.Count);
    }

    [Fact]
    public void Populate_SetsCountByDomain_IncludingZeroForUnusedDomains()
    {
        var domains = new List<StepDomain>
        {
            new() { Id = "Auth", Label = "Auth" },
            new() { Id = "Billing", Label = "Billing" },
        };
        var steps = new List<Step>
        {
            new() { Id = "s1", Type = "Given", Pattern = "p1", Domain = "Auth" },
            new() { Id = "s2", Type = "Given", Pattern = "p2", Domain = "Auth" },
        };
        var store = new ClientStepLibraryStore();
        store.Populate(MakeLibraryWith(domains, steps));

        Assert.Equal(2, store.CountByDomain["Auth"]);
        Assert.Equal(0, store.CountByDomain["Billing"]);
    }

    [Fact]
    public void Populate_SetsDistinctParamTypes_OrdinalSorted()
    {
        var steps = new List<Step>
        {
            new()
            {
                Id = "s1", Type = "Given", Pattern = "p1", Domain = "Auth",
                Params =
                [
                    new StepParam { Name = "a", Type = "string" },
                    new StepParam { Name = "b", Type = "int" },
                ]
            },
            new()
            {
                Id = "s2", Type = "Given", Pattern = "p2", Domain = "Auth",
                Params =
                [
                    new StepParam { Name = "c", Type = "DocString" },
                    new StepParam { Name = "d", Type = "string" },
                ]
            },
        };
        var store = new ClientStepLibraryStore();
        store.Populate(MakeLibraryWith([], steps));

        Assert.Equal(new[] { "DocString", "int", "string" }, store.DistinctParamTypes);
    }

    [Fact]
    public void Populate_SetsDomainById_WithAllDomains()
    {
        var domains = new List<StepDomain>
        {
            new() { Id = "Auth", Label = "Auth & Identity" },
            new() { Id = "Billing", Label = "Billing" },
            new() { Id = "Reporting", Label = "Reporting" },
        };
        var store = new ClientStepLibraryStore();
        store.Populate(MakeLibraryWith(domains, []));

        Assert.Equal(3, store.DomainById.Count);
        Assert.Equal("Auth & Identity", store.DomainById["Auth"].Label);
        Assert.Equal("Billing", store.DomainById["Billing"].Label);
        Assert.Equal("Reporting", store.DomainById["Reporting"].Label);
    }

    [Fact]
    public void Populate_SecondCall_IsNoOp_DomainByIdRetainsFirst()
    {
        // Populate is idempotent — a second call is silently ignored.
        var first = new List<StepDomain>
        {
            new() { Id = "Auth", Label = "Auth" },
            new() { Id = "Billing", Label = "Billing" },
        };
        var second = new List<StepDomain>
        {
            new() { Id = "Reporting", Label = "Reporting" },
        };
        var store = new ClientStepLibraryStore();
        store.Populate(MakeLibraryWith(first, []));
        store.Populate(MakeLibraryWith(second, [])); // no-op

        Assert.Equal(2, store.DomainById.Count);
        Assert.True(store.DomainById.ContainsKey("Auth"));
        Assert.True(store.DomainById.ContainsKey("Billing"));
        Assert.False(store.DomainById.ContainsKey("Reporting"));
    }

    [Fact]
    public void DomainById_Defaults_To_Empty_Before_Populate()
    {
        var store = new ClientStepLibraryStore();
        Assert.Empty(store.DomainById);
        Assert.False(store.DomainById.TryGetValue("Auth", out _));
    }

    [Fact]
    public void Populate_SetsHaystacks_KeyedByStepId_IncludingPatternTypeDomainTagsAndParamNames()
    {
        var step = new Step
        {
            Id = "s-login",
            Type = "Given",
            Pattern = "I am logged in as {string}",
            Domain = "Auth",
            Tags = new[] { "login" },
            Params = new[] { new StepParam { Name = "username", Type = "string" } },
        };
        var store = new ClientStepLibraryStore();
        store.Populate(MakeLibraryWith([], new[] { step }));

        Assert.True(store.Haystacks.ContainsKey("s-login"));
        var hay = store.Haystacks["s-login"];
        Assert.Contains("logged in", hay);
        Assert.Contains("Given", hay);
        Assert.Contains("Auth", hay);
        Assert.Contains("login", hay);
        Assert.Contains("username", hay);
    }

    [Fact]
    public void Populate_SecondCall_IsNoOp()
    {
        var store = new ClientStepLibraryStore();
        var lib1 = MakeLibraryWith([], [new Step { Id = "s1", Type = "Given", Pattern = "p1", Domain = "Auth" }]);
        var lib2 = MakeLibraryWith([], [new Step { Id = "s2", Type = "Given", Pattern = "p2", Domain = "Auth" }]);

        store.Populate(lib1);
        store.Populate(lib2); // should be ignored

        Assert.Single(store.Steps);
        Assert.Equal("s1", store.Steps[0].Id);
    }
}
