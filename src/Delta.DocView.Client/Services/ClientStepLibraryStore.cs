using Delta.DocView.Shared.Models;

namespace Delta.DocView.Client.Services;

public sealed class ClientStepLibraryStore
{
    public IReadOnlyList<Step> Steps { get; private set; } = [];
    public IReadOnlyList<StepDomain> Domains { get; private set; } = [];
    public IReadOnlyDictionary<string, Step> ById { get; private set; } =
        new Dictionary<string, Step>();
    public string Version { get; private set; } = "";
    public string GeneratedAt { get; private set; } = "";

    public void Populate(StepLibrary library)
    {
        Steps = library.Steps;
        Domains = library.Domains;
        ById = library.Steps.ToDictionary(s => s.Id);
        Version = library.Version;
        GeneratedAt = library.GeneratedAt;
    }
}
