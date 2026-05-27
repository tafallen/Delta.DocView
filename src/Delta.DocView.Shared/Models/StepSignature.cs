using System.Text.Json.Serialization;

namespace Delta.DocView.Shared.Models;

public sealed class StepSignature
{
    [JsonPropertyName("algorithm")]
    public string Algorithm { get; init; } = "";

    [JsonPropertyName("digest")]
    public string Digest { get; init; } = "";
}
