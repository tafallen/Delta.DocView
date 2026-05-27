using System.Text.Json.Serialization;

namespace Delta.DocView.Shared.Models;

public sealed class StepLibrary
{
    [JsonPropertyName("$schema")]
    public string Schema { get; init; } = "";

    [JsonPropertyName("version")]
    public string Version { get; init; } = "";

    [JsonPropertyName("generatedAt")]
    public string GeneratedAt { get; init; } = "";

    [JsonPropertyName("generatorVersion")]
    public string GeneratorVersion { get; init; } = "";

    [JsonPropertyName("enriched")]
    public bool Enriched { get; init; }

    [JsonPropertyName("domains")]
    public IReadOnlyList<StepDomain> Domains { get; init; } = [];

    [JsonPropertyName("steps")]
    public IReadOnlyList<Step> Steps { get; init; } = [];

    [JsonPropertyName("signature")]
    public StepSignature Signature { get; init; } = new();
}
