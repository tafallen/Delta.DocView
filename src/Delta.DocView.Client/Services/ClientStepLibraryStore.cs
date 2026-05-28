using Delta.DocView.Shared.Models;

namespace Delta.DocView.Client.Services;

public sealed class ClientStepLibraryStore
{
    public IReadOnlyList<Step> Steps { get; private set; } = [];
    public IReadOnlyList<StepDomain> Domains { get; private set; } = [];
    public IReadOnlyDictionary<string, Step> ById { get; private set; } =
        new Dictionary<string, Step>();
    public IReadOnlyDictionary<string, int> CountByType { get; private set; } =
        new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> CountByDomain { get; private set; } =
        new Dictionary<string, int>();
    public IReadOnlyDictionary<string, StepDomain> DomainById { get; private set; } =
        new Dictionary<string, StepDomain>();
    public IReadOnlyList<string> DistinctParamTypes { get; private set; } = [];
    public string Version { get; private set; } = "";
    public string GeneratedAt { get; private set; } = "";

    public void Populate(StepLibrary library)
    {
        Steps = library.Steps;
        Domains = library.Domains;
        ById = library.Steps.ToDictionary(s => s.Id);
        DomainById = library.Domains.ToDictionary(d => d.Id);

        var countByType = GherkinStepTypes.All.ToDictionary(t => t, _ => 0);
        foreach (var step in library.Steps)
        {
            if (countByType.ContainsKey(step.Type))
            {
                countByType[step.Type]++;
            }
        }
        CountByType = countByType;

        var countByDomain = library.Domains.ToDictionary(d => d.Id, _ => 0);
        foreach (var step in library.Steps)
        {
            if (countByDomain.ContainsKey(step.Domain))
            {
                countByDomain[step.Domain]++;
            }
        }
        CountByDomain = countByDomain;

        DistinctParamTypes = library.Steps
            .SelectMany(s => s.Params)
            .Select(p => p.Type)
            .Distinct()
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();

        Version = library.Version;
        GeneratedAt = library.GeneratedAt;
    }
}
