using System.Text.Json.Serialization;

namespace Delta.DocView.Shared.Models;

public sealed class StepParam
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("example")]
    public string Example { get; init; } = "";
}
