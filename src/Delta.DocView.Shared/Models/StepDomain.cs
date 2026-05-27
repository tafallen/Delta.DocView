using System.Text.Json.Serialization;

namespace Delta.DocView.Shared.Models;

public sealed class StepDomain
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("label")]
    public string Label { get; init; } = "";
}
