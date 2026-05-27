using System.Text.Json;
using Delta.DocView.Shared.Models;

namespace Delta.DocView.Server.Services;

public sealed class StepLibraryLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false
    };

    public (StepLibrary Library, string RawJson) Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Step library file not found at '{path}'.", path);

        var rawJson = File.ReadAllText(path);
        var library = JsonSerializer.Deserialize<StepLibrary>(rawJson, Options)
            ?? throw new InvalidOperationException("Deserialisation returned null.");

        return (library, rawJson);
    }
}
