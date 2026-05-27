using Delta.DocView.Server.Services;

namespace Delta.DocView.Tests.Services;

public class StartupLoaderTests
{
    private static readonly string TestDataDir =
        Path.Combine(AppContext.BaseDirectory, "TestData");

    [Fact]
    public void Run_ValidFileCorrectDigest_NoErrorNoWarning()
    {
        var path = WriteTempFile(BuildValidLibraryJsonWithCorrectDigest());
        var (error, store) = CreateDeps();
        try
        {
            StartupLoader.Run(path, new StepLibraryLoader(), new StepLibraryValidator(), error, store);

            Assert.False(error.HasError);
            Assert.False(error.HasWarning);
            Assert.True(store.IsLoaded);
            Assert.Single(store.Library!.Steps);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Run_MissingFile_SetsError_StoreNotLoaded()
    {
        var (error, store) = CreateDeps();

        StartupLoader.Run("/no/such/file.json",
            new StepLibraryLoader(), new StepLibraryValidator(), error, store);

        Assert.True(error.HasError);
        Assert.Contains("/no/such/file.json", error.ErrorMessage);
        Assert.False(store.IsLoaded);
    }

    [Fact]
    public void Run_InvalidSchema_SetsError_StoreNotLoaded()
    {
        var path = Path.Combine(TestDataDir, "invalid-library.json");
        var (error, store) = CreateDeps();

        StartupLoader.Run(path, new StepLibraryLoader(), new StepLibraryValidator(), error, store);

        Assert.True(error.HasError);
        Assert.False(store.IsLoaded);
    }

    [Fact]
    public void Run_WrongDigest_SetsWarning_StoreIsLoaded()
    {
        // valid-library.json has a placeholder digest "000...0" which won't match
        var path = Path.Combine(TestDataDir, "valid-library.json");
        var (error, store) = CreateDeps();

        StartupLoader.Run(path, new StepLibraryLoader(), new StepLibraryValidator(), error, store);

        Assert.False(error.HasError);
        Assert.True(error.HasWarning);
        Assert.True(store.IsLoaded);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static (StartupError error, StepLibraryStore store) CreateDeps() =>
        (new StartupError(), new StepLibraryStore());

    private static string WriteTempFile(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        return path;
    }

    private static string BuildValidLibraryJsonWithCorrectDigest()
    {
        // Body without signature property
        var body = """
            {
              "$schema": "https://delta.docgen/schema/v1/step-library.schema.json",
              "version": "1.0.0",
              "generatedAt": "2026-01-01T00:00:00Z",
              "generatorVersion": "1.0.0",
              "enriched": true,
              "domains": [{ "id": "Auth", "label": "Auth & Identity" }],
              "steps": [{
                "id": "auth-001a2b3c",
                "type": "Given",
                "pattern": "I am logged in as {string}",
                "params": [{ "name": "username", "type": "string", "example": "\"admin@delta.io\"" }],
                "file": "Auth/AuthSteps.cs",
                "line": 10,
                "domain": "Auth",
                "tags": ["login"],
                "used": 100,
                "description": "Logs in.",
                "source": "public void Login(string u) {}",
                "suggestsNext": []
              }]
            }
            """;

        // Compute digest over canonical form (body parsed then re-serialised without signature)
        using var doc = System.Text.Json.JsonDocument.Parse(body);
        using var ms = new System.IO.MemoryStream();
        using var writer = new System.Text.Json.Utf8JsonWriter(ms);
        writer.WriteStartObject();
        foreach (var p in doc.RootElement.EnumerateObject())
            p.WriteTo(writer);
        writer.WriteEndObject();
        writer.Flush();
        var digest = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(ms.ToArray())).ToLowerInvariant();

        return $$"""
            {
              "$schema": "https://delta.docgen/schema/v1/step-library.schema.json",
              "version": "1.0.0",
              "generatedAt": "2026-01-01T00:00:00Z",
              "generatorVersion": "1.0.0",
              "enriched": true,
              "domains": [{ "id": "Auth", "label": "Auth & Identity" }],
              "steps": [{
                "id": "auth-001a2b3c",
                "type": "Given",
                "pattern": "I am logged in as {string}",
                "params": [{ "name": "username", "type": "string", "example": "\"admin@delta.io\"" }],
                "file": "Auth/AuthSteps.cs",
                "line": 10,
                "domain": "Auth",
                "tags": ["login"],
                "used": 100,
                "description": "Logs in.",
                "source": "public void Login(string u) {}",
                "suggestsNext": []
              }],
              "signature": { "algorithm": "SHA-256", "digest": "{{digest}}" }
            }
            """;
    }
}
