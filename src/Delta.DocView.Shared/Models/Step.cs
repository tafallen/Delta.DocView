using System.Text.Json.Serialization;

namespace Delta.DocView.Shared.Models;

public sealed class Step
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("pattern")]
    public string Pattern { get; init; } = "";

    [JsonPropertyName("params")]
    public IReadOnlyList<StepParam> Params { get; init; } = [];

    [JsonPropertyName("file")]
    public string File { get; init; } = "";

    [JsonPropertyName("line")]
    public int Line { get; init; }

    [JsonPropertyName("domain")]
    public string Domain { get; init; } = "";

    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = [];

    [JsonPropertyName("used")]
    public int Used { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    [JsonPropertyName("source")]
    public string Source { get; init; } = "";

    [JsonPropertyName("suggestsNext")]
    public IReadOnlyList<string> SuggestsNext { get; init; } = [];
}
