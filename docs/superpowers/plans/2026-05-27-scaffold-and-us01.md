# Delta.DocView — Solution Scaffold + US-01 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Scaffold the 3-project Blazor WASM hosted solution and implement everything needed for the app to start, load and validate the step-library JSON on the server, serve it to an authenticated client, and render a loading screen / error page / main layout depending on the outcome.

**Architecture:** `Delta.DocView.Shared` holds domain models used by both sides. `Delta.DocView.Server` is an ASP.NET Core host that loads the JSON at startup into a singleton store and serves it via `GET /api/library`. `Delta.DocView.Client` is a Blazor WASM app that fetches the library once on boot, holds it in a client-side singleton, and renders a loading screen during the fetch and an error page on failure.

**Tech Stack:** .NET 8, C# 12, Blazor WebAssembly (hosted), System.Text.Json, JsonSchema.Net, xUnit, bUnit, NSubstitute

---

## File Map

```
Delta.DocView.sln
src/
  Delta.DocView.Shared/
    Delta.DocView.Shared.csproj
    Models/
      StepLibrary.cs
      Step.cs
      StepParam.cs
      StepDomain.cs
      StepSignature.cs
    LibraryResponse.cs              ← API DTO shared between server and client

  Delta.DocView.Server/
    Delta.DocView.Server.csproj
    Program.cs                      ← startup wiring
    Schemas/
      step-library.v1.schema.json   ← embedded resource (copied from repo root)
    Services/
      IStepLibraryStore.cs
      StepLibraryStore.cs           ← singleton populated at startup
      IStartupError.cs
      StartupError.cs               ← singleton; records error or warning
      StepLibraryLoader.cs          ← file I/O + deserialise
      StepLibraryValidator.cs       ← JsonSchema.Net validation
      SignatureVerifier.cs          ← SHA-256 digest check
      StartupLoader.cs              ← orchestrate load → validate → verify → store
    Controllers/
      LibraryController.cs          ← GET /api/library
      HealthController.cs           ← GET /health

  Delta.DocView.Client/
    Delta.DocView.Client.csproj
    Program.cs                      ← register services, configure HttpClient
    _Imports.razor
    App.razor                       ← root; branches on LoadingState
    Services/
      LoadingState.cs               ← enum: Loading / Loaded / Error
      LibraryApiClient.cs           ← fetches /api/library once; exposes LoadingState
      ClientStepLibraryStore.cs     ← singleton; holds steps in memory
    Components/
      LoadingScreen.razor
      StartupErrorPage.razor
    wwwroot/
      index.html                    ← static loading screen (pre-WASM-boot)

tests/
  Delta.DocView.Tests/
    Delta.DocView.Tests.csproj
    Services/
      StepLibraryLoaderTests.cs
      StepLibraryValidatorTests.cs
      SignatureVerifierTests.cs
      StartupLoaderTests.cs
      LibraryApiClientTests.cs
    Components/
      LoadingScreenTests.cs
      StartupErrorPageTests.cs
    TestData/
      valid-library.json
      invalid-library.json
```

---

## Task 0: Scaffold the solution

**Files:** Everything in the file map above (structure only — content comes in later tasks)

- [ ] **Step 1: Create the solution and three source projects**

```powershell
cd C:\repos\Delta.DocView

dotnet new sln -n Delta.DocView

dotnet new classlib -o src/Delta.DocView.Shared -n Delta.DocView.Shared -f net8.0
dotnet new web    -o src/Delta.DocView.Server -n Delta.DocView.Server -f net8.0
dotnet new blazorwasm -o src/Delta.DocView.Client -n Delta.DocView.Client -f net8.0

dotnet sln add src/Delta.DocView.Shared/Delta.DocView.Shared.csproj
dotnet sln add src/Delta.DocView.Server/Delta.DocView.Server.csproj
dotnet sln add src/Delta.DocView.Client/Delta.DocView.Client.csproj
```

- [ ] **Step 2: Create the test project and add all references**

```powershell
dotnet new xunit -o tests/Delta.DocView.Tests -n Delta.DocView.Tests -f net8.0
dotnet sln add tests/Delta.DocView.Tests/Delta.DocView.Tests.csproj

dotnet add src/Delta.DocView.Server reference src/Delta.DocView.Shared/Delta.DocView.Shared.csproj
dotnet add src/Delta.DocView.Client reference src/Delta.DocView.Shared/Delta.DocView.Shared.csproj

dotnet add tests/Delta.DocView.Tests reference src/Delta.DocView.Shared/Delta.DocView.Shared.csproj
dotnet add tests/Delta.DocView.Tests reference src/Delta.DocView.Server/Delta.DocView.Server.csproj
dotnet add tests/Delta.DocView.Tests reference src/Delta.DocView.Client/Delta.DocView.Client.csproj
```

- [ ] **Step 3: Add NuGet packages**

```powershell
dotnet add src/Delta.DocView.Server package JsonSchema.Net --version 7.*
dotnet add src/Delta.DocView.Server package Microsoft.AspNetCore.Components.WebAssembly.Server --version 8.*

dotnet add tests/Delta.DocView.Tests package bunit --version 1.*
dotnet add tests/Delta.DocView.Tests package NSubstitute --version 5.*
dotnet add tests/Delta.DocView.Tests package Microsoft.AspNetCore.Components.Web --version 8.*
```

- [ ] **Step 4: Configure the Server to host the Client WASM app**

Open `src/Delta.DocView.Server/Delta.DocView.Server.csproj` and replace the entire contents with:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="JsonSchema.Net" Version="7.*" />
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.Server" Version="8.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Delta.DocView.Shared\Delta.DocView.Shared.csproj" />
    <ProjectReference Include="..\Delta.DocView.Client\Delta.DocView.Client.csproj" />
  </ItemGroup>
  <ItemGroup>
    <EmbeddedResource Include="Schemas\step-library.v1.schema.json"
                      LogicalName="step-library.v1.schema.json" />
  </ItemGroup>
</Project>
```

> Note: the Server also references the Client project so the WASM files are included in the Server's output and served via `UseBlazorFrameworkFiles()`.

- [ ] **Step 5: Copy the schema file into the Server project**

```powershell
New-Item -ItemType Directory -Path src/Delta.DocView.Server/Schemas -Force
Copy-Item step-library.v1.schema.json src/Delta.DocView.Server/Schemas/step-library.v1.schema.json
```

- [ ] **Step 6: Replace the generated Server Program.cs with a minimal stub**

```csharp
// src/Delta.DocView.Server/Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

var app = builder.Build();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
```

- [ ] **Step 7: Delete generated boilerplate files and the placeholder test**

```powershell
Remove-Item src/Delta.DocView.Shared/Class1.cs -ErrorAction SilentlyContinue
Remove-Item tests/Delta.DocView.Tests/UnitTest1.cs -ErrorAction SilentlyContinue
```

- [ ] **Step 8: Verify the solution builds**

```powershell
dotnet build Delta.DocView.sln
```

Expected: `Build succeeded. 0 Error(s)` (warnings about empty projects are fine at this stage).

- [ ] **Step 9: Mark TestData files as copy-to-output in the test project**

Open `tests/Delta.DocView.Tests/Delta.DocView.Tests.csproj` and add inside `<Project>`:

```xml
<ItemGroup>
  <None Update="TestData\**\*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

- [ ] **Step 10: Create the TestData directory and placeholder files**

```powershell
New-Item -ItemType Directory -Path tests/Delta.DocView.Tests/TestData -Force
New-Item -ItemType Directory -Path tests/Delta.DocView.Tests/Services -Force
New-Item -ItemType Directory -Path tests/Delta.DocView.Tests/Components -Force
```

- [ ] **Step 11: Commit the scaffold**

```powershell
git add -A
git commit -m "chore: scaffold 3-project Blazor WASM hosted solution"
```

---

## Task 1: Shared models

**Files:**
- Create: `src/Delta.DocView.Shared/Models/StepParam.cs`
- Create: `src/Delta.DocView.Shared/Models/StepDomain.cs`
- Create: `src/Delta.DocView.Shared/Models/StepSignature.cs`
- Create: `src/Delta.DocView.Shared/Models/Step.cs`
- Create: `src/Delta.DocView.Shared/Models/StepLibrary.cs`
- Create: `src/Delta.DocView.Shared/LibraryResponse.cs`

No failing test for pure data models — correctness is verified by the deserialisation tests in Task 2.

- [ ] **Step 1: Create `src/Delta.DocView.Shared/Models/StepParam.cs`**

```csharp
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
```

- [ ] **Step 2: Create `src/Delta.DocView.Shared/Models/StepDomain.cs`**

```csharp
using System.Text.Json.Serialization;

namespace Delta.DocView.Shared.Models;

public sealed class StepDomain
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("label")]
    public string Label { get; init; } = "";
}
```

- [ ] **Step 3: Create `src/Delta.DocView.Shared/Models/StepSignature.cs`**

```csharp
using System.Text.Json.Serialization;

namespace Delta.DocView.Shared.Models;

public sealed class StepSignature
{
    [JsonPropertyName("algorithm")]
    public string Algorithm { get; init; } = "";

    [JsonPropertyName("digest")]
    public string Digest { get; init; } = "";
}
```

- [ ] **Step 4: Create `src/Delta.DocView.Shared/Models/Step.cs`**

```csharp
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
```

- [ ] **Step 5: Create `src/Delta.DocView.Shared/Models/StepLibrary.cs`**

```csharp
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
```

- [ ] **Step 6: Create `src/Delta.DocView.Shared/LibraryResponse.cs`**

```csharp
using Delta.DocView.Shared.Models;

namespace Delta.DocView.Shared;

/// <summary>
/// Returned by GET /api/library. Null Warning means the signature verified cleanly.
/// </summary>
public sealed record LibraryResponse(StepLibrary Library, string? Warning);
```

- [ ] **Step 7: Build to confirm no errors**

```powershell
dotnet build src/Delta.DocView.Shared/Delta.DocView.Shared.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 8: Commit**

```powershell
git add src/Delta.DocView.Shared/
git commit -m "feat: add shared domain models and LibraryResponse DTO"
```

---

## Task 2: Server — StepLibraryLoader

**Files:**
- Create: `src/Delta.DocView.Server/Services/StepLibraryLoader.cs`
- Create: `tests/Delta.DocView.Tests/TestData/valid-library.json`
- Create: `tests/Delta.DocView.Tests/Services/StepLibraryLoaderTests.cs`

- [ ] **Step 1: Create `tests/Delta.DocView.Tests/TestData/valid-library.json`**

```json
{
  "$schema": "https://delta.docgen/schema/v1/step-library.schema.json",
  "version": "1.0.0",
  "generatedAt": "2026-01-01T00:00:00Z",
  "generatorVersion": "1.0.0",
  "enriched": true,
  "domains": [
    { "id": "Auth", "label": "Auth & Identity" }
  ],
  "steps": [
    {
      "id": "auth-001a2b3c",
      "type": "Given",
      "pattern": "I am logged in as {string}",
      "params": [{ "name": "username", "type": "string", "example": "\"admin@delta.io\"" }],
      "file": "Auth/AuthSteps.cs",
      "line": 10,
      "domain": "Auth",
      "tags": ["login"],
      "used": 100,
      "description": "Logs in as the given user.",
      "source": "public void Login(string u) {}",
      "suggestsNext": []
    }
  ],
  "signature": {
    "algorithm": "SHA-256",
    "digest": "0000000000000000000000000000000000000000000000000000000000000000"
  }
}
```

> The digest is a placeholder — SignatureVerifier tests handle correctness separately.

- [ ] **Step 2: Write the failing tests**

Create `tests/Delta.DocView.Tests/Services/StepLibraryLoaderTests.cs`:

```csharp
using Delta.DocView.Server.Services;

namespace Delta.DocView.Tests.Services;

public class StepLibraryLoaderTests
{
    private static readonly string TestDataDir =
        Path.Combine(AppContext.BaseDirectory, "TestData");

    [Fact]
    public void Load_ValidFile_ReturnsLibraryAndRawJson()
    {
        var path = Path.Combine(TestDataDir, "valid-library.json");
        var loader = new StepLibraryLoader();

        var (library, rawJson) = loader.Load(path);

        Assert.Equal("1.0.0", library.Version);
        Assert.Single(library.Steps);
        Assert.Equal("auth-001a2b3c", library.Steps[0].Id);
        Assert.NotEmpty(rawJson);
    }

    [Fact]
    public void Load_MissingFile_ThrowsFileNotFoundException()
    {
        var loader = new StepLibraryLoader();

        var ex = Assert.Throws<FileNotFoundException>(
            () => loader.Load("/nonexistent/path/library.json"));

        Assert.Contains("/nonexistent/path/library.json", ex.Message);
    }

    [Fact]
    public void Load_InvalidJson_ThrowsJsonException()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "this is not json {{{");
            var loader = new StepLibraryLoader();

            Assert.Throws<System.Text.Json.JsonException>(() => loader.Load(path));
        }
        finally { File.Delete(path); }
    }
}
```

- [ ] **Step 3: Run the tests — expect FAIL**

```powershell
dotnet test tests/Delta.DocView.Tests --filter "StepLibraryLoaderTests" -v normal
```

Expected: `Failed! - Failed: 3` with errors about missing types.

- [ ] **Step 4: Create `src/Delta.DocView.Server/Services/StepLibraryLoader.cs`**

```csharp
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
```

- [ ] **Step 5: Run the tests — expect PASS**

```powershell
dotnet test tests/Delta.DocView.Tests --filter "StepLibraryLoaderTests" -v normal
```

Expected: `Passed! - Failed: 0, Passed: 3`

- [ ] **Step 6: Commit**

```powershell
git add src/Delta.DocView.Server/Services/StepLibraryLoader.cs `
        tests/Delta.DocView.Tests/Services/StepLibraryLoaderTests.cs `
        tests/Delta.DocView.Tests/TestData/valid-library.json `
        tests/Delta.DocView.Tests/Delta.DocView.Tests.csproj
git commit -m "feat: add StepLibraryLoader"
```

---

## Task 3: Server — StepLibraryValidator

**Files:**
- Create: `src/Delta.DocView.Server/Services/ValidationResult.cs`
- Create: `src/Delta.DocView.Server/Services/StepLibraryValidator.cs`
- Create: `tests/Delta.DocView.Tests/TestData/invalid-library.json`
- Create: `tests/Delta.DocView.Tests/Services/StepLibraryValidatorTests.cs`

- [ ] **Step 1: Create `tests/Delta.DocView.Tests/TestData/invalid-library.json`**

Missing the required `version` field:

```json
{
  "$schema": "https://delta.docgen/schema/v1/step-library.schema.json",
  "generatedAt": "2026-01-01T00:00:00Z",
  "generatorVersion": "1.0.0",
  "enriched": true,
  "domains": [],
  "steps": [],
  "signature": {
    "algorithm": "SHA-256",
    "digest": "0000000000000000000000000000000000000000000000000000000000000000"
  }
}
```

- [ ] **Step 2: Write the failing tests**

Create `tests/Delta.DocView.Tests/Services/StepLibraryValidatorTests.cs`:

```csharp
using Delta.DocView.Server.Services;

namespace Delta.DocView.Tests.Services;

public class StepLibraryValidatorTests
{
    private static readonly string TestDataDir =
        Path.Combine(AppContext.BaseDirectory, "TestData");

    [Fact]
    public void Validate_ValidJson_IsValidTrue_NoErrors()
    {
        var json = File.ReadAllText(Path.Combine(TestDataDir, "valid-library.json"));
        var validator = new StepLibraryValidator();

        var result = validator.Validate(json);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_MissingRequiredField_IsValidFalse_HasErrors()
    {
        var json = File.ReadAllText(Path.Combine(TestDataDir, "invalid-library.json"));
        var validator = new StepLibraryValidator();

        var result = validator.Validate(json);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Validate_MultipleViolations_ErrorsLimitedToFive()
    {
        var json = """
            {
              "$schema": "x",
              "generatedAt": "not-a-datetime",
              "generatorVersion": "bad",
              "enriched": "not-a-bool",
              "domains": [],
              "steps": [],
              "signature": { "algorithm": "SHA-256", "digest": "tooshort" }
            }
            """;
        var validator = new StepLibraryValidator();

        var result = validator.Validate(json);

        Assert.False(result.IsValid);
        Assert.InRange(result.Errors.Count, 1, 5);
    }
}
```

- [ ] **Step 3: Run the tests — expect FAIL**

```powershell
dotnet test tests/Delta.DocView.Tests --filter "StepLibraryValidatorTests" -v normal
```

Expected: `Failed! - Failed: 3`

- [ ] **Step 4: Create `src/Delta.DocView.Server/Services/ValidationResult.cs`**

```csharp
namespace Delta.DocView.Server.Services;

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Ok() => new(true, []);
}
```

- [ ] **Step 5: Create `src/Delta.DocView.Server/Services/StepLibraryValidator.cs`**

```csharp
using System.Reflection;
using System.Text.Json;
using Json.Schema;

namespace Delta.DocView.Server.Services;

public sealed class StepLibraryValidator
{
    private readonly JsonSchema _schema;

    public StepLibraryValidator()
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream("step-library.v1.schema.json")
            ?? throw new InvalidOperationException(
                "Embedded resource 'step-library.v1.schema.json' not found. " +
                "Verify the LogicalName in Delta.DocView.Server.csproj.");
        _schema = JsonSchema.FromText(new StreamReader(stream).ReadToEnd());
    }

    public ValidationResult Validate(string rawJson)
    {
        var element = JsonDocument.Parse(rawJson).RootElement;
        var options = new EvaluationOptions { OutputFormat = OutputFormat.List };
        var result = _schema.Evaluate(element, options);

        if (result.IsValid)
            return ValidationResult.Ok();

        var errors = result.Details
            .Where(d => !d.IsValid && d.Errors is { Count: > 0 })
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))
            .Distinct()
            .Take(5)
            .ToList();

        if (errors.Count == 0)
            errors = ["Schema validation failed."];

        return new ValidationResult(false, errors);
    }
}
```

- [ ] **Step 6: Run the tests — expect PASS**

```powershell
dotnet test tests/Delta.DocView.Tests --filter "StepLibraryValidatorTests" -v normal
```

Expected: `Passed! - Failed: 0, Passed: 3`

- [ ] **Step 7: Commit**

```powershell
git add src/Delta.DocView.Server/Services/ValidationResult.cs `
        src/Delta.DocView.Server/Services/StepLibraryValidator.cs `
        tests/Delta.DocView.Tests/Services/StepLibraryValidatorTests.cs `
        tests/Delta.DocView.Tests/TestData/invalid-library.json
git commit -m "feat: add StepLibraryValidator (JsonSchema.Net, embedded schema)"
```

---

## Task 4: Server — SignatureVerifier

**Files:**
- Create: `src/Delta.DocView.Server/Services/SignatureVerifier.cs`
- Create: `tests/Delta.DocView.Tests/Services/SignatureVerifierTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Delta.DocView.Tests/Services/SignatureVerifierTests.cs`:

```csharp
using System.Security.Cryptography;
using System.Text.Json;
using Delta.DocView.Server.Services;

namespace Delta.DocView.Tests.Services;

public class SignatureVerifierTests
{
    // Reproduces the same algorithm as the implementation so tests are self-contained.
    private static string ComputeDigest(string rawJson)
    {
        using var doc = JsonDocument.Parse(rawJson);
        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms);
        writer.WriteStartObject();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Name == "signature") continue;
            prop.WriteTo(writer);
        }
        writer.WriteEndObject();
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(ms.ToArray())).ToLowerInvariant();
    }

    [Fact]
    public void Verify_CorrectDigest_ReturnsTrue()
    {
        var json = """{"version":"1.0.0","generatedAt":"2026-01-01T00:00:00Z"}""";
        var digest = ComputeDigest(json + ""","signature":{"algorithm":"SHA-256","digest":"x"}""");

        // Build full JSON with that correct digest
        var fullJson = $$"""{"version":"1.0.0","generatedAt":"2026-01-01T00:00:00Z","signature":{"algorithm":"SHA-256","digest":"{{digest}}"}}""";

        Assert.True(SignatureVerifier.Verify(fullJson, digest));
    }

    [Fact]
    public void Verify_WrongDigest_ReturnsFalse()
    {
        var json = """{"version":"1.0.0","signature":{"algorithm":"SHA-256","digest":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}}""";

        Assert.False(SignatureVerifier.Verify(json,
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
    }

    [Fact]
    public void Verify_NoSignatureProperty_HashesPayloadAsIs()
    {
        var json = """{"key":"value"}""";
        var expected = ComputeDigest(json);

        Assert.True(SignatureVerifier.Verify(json, expected));
    }
}
```

- [ ] **Step 2: Run the tests — expect FAIL**

```powershell
dotnet test tests/Delta.DocView.Tests --filter "SignatureVerifierTests" -v normal
```

Expected: `Failed! - Failed: 3`

- [ ] **Step 3: Create `src/Delta.DocView.Server/Services/SignatureVerifier.cs`**

```csharp
using System.Security.Cryptography;
using System.Text.Json;

namespace Delta.DocView.Server.Services;

public static class SignatureVerifier
{
    public static bool Verify(string rawJson, string expectedDigest)
    {
        using var doc = JsonDocument.Parse(rawJson);
        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms);

        writer.WriteStartObject();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Name == "signature") continue;
            prop.WriteTo(writer);
        }
        writer.WriteEndObject();
        writer.Flush();

        var actualDigest = Convert.ToHexString(SHA256.HashData(ms.ToArray()))
            .ToLowerInvariant();

        return actualDigest == expectedDigest;
    }
}
```

- [ ] **Step 4: Run the tests — expect PASS**

```powershell
dotnet test tests/Delta.DocView.Tests --filter "SignatureVerifierTests" -v normal
```

Expected: `Passed! - Failed: 0, Passed: 3`

- [ ] **Step 5: Commit**

```powershell
git add src/Delta.DocView.Server/Services/SignatureVerifier.cs `
        tests/Delta.DocView.Tests/Services/SignatureVerifierTests.cs
git commit -m "feat: add SignatureVerifier (SHA-256 of JSON without signature property)"
```

---

## Task 5: Server — StartupError, StepLibraryStore, StartupLoader

**Files:**
- Create: `src/Delta.DocView.Server/Services/IStartupError.cs`
- Create: `src/Delta.DocView.Server/Services/StartupError.cs`
- Create: `src/Delta.DocView.Server/Services/IStepLibraryStore.cs`
- Create: `src/Delta.DocView.Server/Services/StepLibraryStore.cs`
- Create: `src/Delta.DocView.Server/Services/StartupLoader.cs`
- Create: `tests/Delta.DocView.Tests/Services/StartupLoaderTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Delta.DocView.Tests/Services/StartupLoaderTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the tests — expect FAIL**

```powershell
dotnet test tests/Delta.DocView.Tests --filter "StartupLoaderTests" -v normal
```

Expected: `Failed! - Failed: 4`

- [ ] **Step 3: Create `src/Delta.DocView.Server/Services/IStartupError.cs`**

```csharp
namespace Delta.DocView.Server.Services;

public interface IStartupError
{
    bool HasError { get; }
    string? ErrorMessage { get; }
    bool HasWarning { get; }
    string? WarningMessage { get; }
}
```

- [ ] **Step 4: Create `src/Delta.DocView.Server/Services/StartupError.cs`**

```csharp
namespace Delta.DocView.Server.Services;

public sealed class StartupError : IStartupError
{
    public bool HasError { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool HasWarning { get; private set; }
    public string? WarningMessage { get; private set; }

    public void SetError(string message) { HasError = true; ErrorMessage = message; }
    public void SetWarning(string message) { HasWarning = true; WarningMessage = message; }
}
```

- [ ] **Step 5: Create `src/Delta.DocView.Server/Services/IStepLibraryStore.cs`**

```csharp
using Delta.DocView.Shared.Models;

namespace Delta.DocView.Server.Services;

public interface IStepLibraryStore
{
    bool IsLoaded { get; }
    StepLibrary? Library { get; }
}
```

- [ ] **Step 6: Create `src/Delta.DocView.Server/Services/StepLibraryStore.cs`**

```csharp
using Delta.DocView.Shared.Models;

namespace Delta.DocView.Server.Services;

public sealed class StepLibraryStore : IStepLibraryStore
{
    public bool IsLoaded { get; private set; }
    public StepLibrary? Library { get; private set; }

    public void Populate(StepLibrary library)
    {
        Library = library;
        IsLoaded = true;
    }
}
```

- [ ] **Step 7: Create `src/Delta.DocView.Server/Services/StartupLoader.cs`**

```csharp
namespace Delta.DocView.Server.Services;

public static class StartupLoader
{
    public static void Run(
        string libraryPath,
        StepLibraryLoader loader,
        StepLibraryValidator validator,
        StartupError error,
        StepLibraryStore store)
    {
        string rawJson;
        Shared.Models.StepLibrary library;

        try
        {
            (library, rawJson) = loader.Load(libraryPath);
        }
        catch (FileNotFoundException ex)
        {
            error.SetError(
                $"Step library file not found at '{libraryPath}'. " +
                $"Set DOCVIEW_LIBRARY_PATH to the correct path. ({ex.Message})");
            return;
        }
        catch (Exception ex)
        {
            error.SetError($"Failed to read step library: {ex.Message}");
            return;
        }

        var validation = validator.Validate(rawJson);
        if (!validation.IsValid)
        {
            error.SetError("Schema validation failed:\n• " +
                string.Join("\n• ", validation.Errors));
            return;
        }

        if (!SignatureVerifier.Verify(rawJson, library.Signature.Digest))
        {
            error.SetWarning(
                "Step library signature mismatch — the file may have been modified " +
                "after generation. The library is loaded but integrity cannot be guaranteed.");
        }

        store.Populate(library);
    }
}
```

- [ ] **Step 8: Run the tests — expect PASS**

```powershell
dotnet test tests/Delta.DocView.Tests --filter "StartupLoaderTests" -v normal
```

Expected: `Passed! - Failed: 0, Passed: 4`

- [ ] **Step 9: Commit**

```powershell
git add src/Delta.DocView.Server/Services/IStartupError.cs `
        src/Delta.DocView.Server/Services/StartupError.cs `
        src/Delta.DocView.Server/Services/IStepLibraryStore.cs `
        src/Delta.DocView.Server/Services/StepLibraryStore.cs `
        src/Delta.DocView.Server/Services/StartupLoader.cs `
        tests/Delta.DocView.Tests/Services/StartupLoaderTests.cs
git commit -m "feat: add StartupError, StepLibraryStore, StartupLoader"
```

---

## Task 6: Server — Controllers and Program.cs wiring

**Files:**
- Create: `src/Delta.DocView.Server/Controllers/LibraryController.cs`
- Create: `src/Delta.DocView.Server/Controllers/HealthController.cs`
- Modify: `src/Delta.DocView.Server/Program.cs`

No unit tests for controllers in this plan (they are thin wrappers around singletons already tested). The client-side integration in Task 8 exercises the full round-trip.

- [ ] **Step 1: Create `src/Delta.DocView.Server/Controllers/HealthController.cs`**

```csharp
using Delta.DocView.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Delta.DocView.Server.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class HealthController : ControllerBase
{
    private readonly IStartupError _error;
    private readonly IStepLibraryStore _store;

    public HealthController(IStartupError error, IStepLibraryStore store)
    {
        _error = error;
        _store = store;
    }

    [HttpGet("/health")]
    public IActionResult Get()
    {
        if (_store.IsLoaded)
            return Ok(new { status = "healthy" });

        return StatusCode(503, new
        {
            status = "unhealthy",
            reason = _error.ErrorMessage ?? "Library not loaded."
        });
    }
}
```

- [ ] **Step 2: Create `src/Delta.DocView.Server/Controllers/LibraryController.cs`**

```csharp
using Delta.DocView.Server.Services;
using Delta.DocView.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Delta.DocView.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class LibraryController : ControllerBase
{
    private readonly IStartupError _error;
    private readonly IStepLibraryStore _store;

    public LibraryController(IStartupError error, IStepLibraryStore store)
    {
        _error = error;
        _store = store;
    }

    [HttpGet]
    public IActionResult Get()
    {
        if (_error.HasError)
            return StatusCode(503, new { error = _error.ErrorMessage });

        var response = new LibraryResponse(
            _store.Library!,
            _error.HasWarning ? _error.WarningMessage : null);

        return Ok(response);
    }
}
```

- [ ] **Step 3: Replace `src/Delta.DocView.Server/Program.cs` with the full wiring**

```csharp
using Delta.DocView.Server.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

// Startup error and library store — singletons read by controllers
builder.Services.AddSingleton<StartupError>();
builder.Services.AddSingleton<IStartupError>(sp => sp.GetRequiredService<StartupError>());
builder.Services.AddSingleton<StepLibraryStore>();
builder.Services.AddSingleton<IStepLibraryStore>(sp => sp.GetRequiredService<StepLibraryStore>());

var app = builder.Build();

// ── Load the step library at startup ────────────────────────────────────────
var libraryPath = app.Configuration["DOCVIEW_LIBRARY_PATH"]
    ?? Path.Combine(app.Environment.ContentRootPath, "data", "step-library.json");

var startupError  = app.Services.GetRequiredService<StartupError>();
var startupStore  = app.Services.GetRequiredService<StepLibraryStore>();

StartupLoader.Run(
    libraryPath,
    new StepLibraryLoader(),
    new StepLibraryValidator(),
    startupError,
    startupStore);

if (startupError.HasError)
    app.Logger.LogError("Startup error: {Error}", startupError.ErrorMessage);
else if (startupError.HasWarning)
    app.Logger.LogWarning("Startup warning: {Warning}", startupError.WarningMessage);
else
    app.Logger.LogInformation(
        "Step library loaded: {Count} steps, version {Version}, generated {GeneratedAt}",
        startupStore.Library!.Steps.Count,
        startupStore.Library.Version,
        startupStore.Library.GeneratedAt);

// ── Middleware ───────────────────────────────────────────────────────────────
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
```

- [ ] **Step 4: Build the Server project**

```powershell
dotnet build src/Delta.DocView.Server/Delta.DocView.Server.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Run the full test suite**

```powershell
dotnet test Delta.DocView.sln
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src/Delta.DocView.Server/Controllers/ `
        src/Delta.DocView.Server/Program.cs
git commit -m "feat: add LibraryController, HealthController, and Program.cs wiring"
```

---

## Task 7: Client — LibraryApiClient and ClientStepLibraryStore

**Files:**
- Create: `src/Delta.DocView.Client/Services/LoadingState.cs`
- Create: `src/Delta.DocView.Client/Services/ClientStepLibraryStore.cs`
- Create: `src/Delta.DocView.Client/Services/LibraryApiClient.cs`
- Create: `tests/Delta.DocView.Tests/Services/LibraryApiClientTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Delta.DocView.Tests/Services/LibraryApiClientTests.cs`:

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using Delta.DocView.Client.Services;
using Delta.DocView.Shared;
using Delta.DocView.Shared.Models;

namespace Delta.DocView.Tests.Services;

public class LibraryApiClientTests
{
    private static readonly StepLibrary SampleLibrary = new()
    {
        Version = "1.0.0",
        GeneratedAt = "2026-01-01T00:00:00Z",
        GeneratorVersion = "1.0.0",
        Domains = [new StepDomain { Id = "Auth", Label = "Auth & Identity" }],
        Steps = [new Step
        {
            Id = "auth-001a2b3c", Type = "Given",
            Pattern = "I am logged in as {string}",
            Params = [new StepParam { Name = "username", Type = "string", Example = "\"admin@delta.io\"" }],
            File = "Auth/AuthSteps.cs", Line = 10, Domain = "Auth",
            Tags = ["login"], Used = 100,
            Description = "Logs in.", Source = "public void Login() {}",
            SuggestsNext = []
        }],
        Signature = new StepSignature { Algorithm = "SHA-256", Digest = new string('0', 64) }
    };

    [Fact]
    public async Task LoadAsync_SuccessResponse_StateBecomesLoaded()
    {
        var response = new LibraryResponse(SampleLibrary, null);
        var http = CreateMockHttpClient(HttpStatusCode.OK, response);
        var store = new ClientStepLibraryStore();
        var client = new LibraryApiClient(http, store);

        await client.LoadAsync();

        Assert.Equal(LoadingState.Loaded, client.State);
        Assert.Null(client.ErrorMessage);
        Assert.Single(store.Steps);
    }

    [Fact]
    public async Task LoadAsync_SuccessResponseWithWarning_StoresWarning()
    {
        var response = new LibraryResponse(SampleLibrary, "Signature mismatch.");
        var http = CreateMockHttpClient(HttpStatusCode.OK, response);
        var store = new ClientStepLibraryStore();
        var client = new LibraryApiClient(http, store);

        await client.LoadAsync();

        Assert.Equal(LoadingState.Loaded, client.State);
        Assert.Equal("Signature mismatch.", client.WarningMessage);
    }

    [Fact]
    public async Task LoadAsync_503Response_StateBecomesError()
    {
        var body = new { error = "Library file not found." };
        var http = CreateMockHttpClient(HttpStatusCode.ServiceUnavailable, body);
        var store = new ClientStepLibraryStore();
        var client = new LibraryApiClient(http, store);

        await client.LoadAsync();

        Assert.Equal(LoadingState.Error, client.State);
        Assert.Contains("Library file not found.", client.ErrorMessage);
    }

    [Fact]
    public async Task LoadAsync_IsIdempotent()
    {
        var response = new LibraryResponse(SampleLibrary, null);
        var http = CreateMockHttpClient(HttpStatusCode.OK, response);
        var store = new ClientStepLibraryStore();
        var client = new LibraryApiClient(http, store);

        await client.LoadAsync();
        await client.LoadAsync(); // second call should be a no-op

        Assert.Equal(LoadingState.Loaded, client.State);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static HttpClient CreateMockHttpClient(HttpStatusCode status, object body)
    {
        var json = JsonSerializer.Serialize(body);
        var handler = new StubHttpMessageHandler(status, json);
        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHttpMessageHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
    }
}
```

- [ ] **Step 2: Run the tests — expect FAIL**

```powershell
dotnet test tests/Delta.DocView.Tests --filter "LibraryApiClientTests" -v normal
```

Expected: `Failed! - Failed: 4`

- [ ] **Step 3: Create `src/Delta.DocView.Client/Services/LoadingState.cs`**

```csharp
namespace Delta.DocView.Client.Services;

public enum LoadingState { Loading, Loaded, Error }
```

- [ ] **Step 4: Create `src/Delta.DocView.Client/Services/ClientStepLibraryStore.cs`**

```csharp
using Delta.DocView.Shared.Models;

namespace Delta.DocView.Client.Services;

public sealed class ClientStepLibraryStore
{
    public IReadOnlyList<Step> Steps { get; private set; } = [];
    public IReadOnlyList<StepDomain> Domains { get; private set; } = [];
    public IReadOnlyDictionary<string, Step> ById { get; private set; } =
        new Dictionary<string, Step>();

    public void Populate(StepLibrary library)
    {
        Steps = library.Steps;
        Domains = library.Domains;
        ById = library.Steps.ToDictionary(s => s.Id);
    }
}
```

- [ ] **Step 5: Create `src/Delta.DocView.Client/Services/LibraryApiClient.cs`**

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using Delta.DocView.Shared;

namespace Delta.DocView.Client.Services;

public sealed class LibraryApiClient
{
    private readonly HttpClient _http;
    private readonly ClientStepLibraryStore _store;

    public LoadingState State { get; private set; } = LoadingState.Loading;
    public string? ErrorMessage { get; private set; }
    public string? WarningMessage { get; private set; }

    public LibraryApiClient(HttpClient http, ClientStepLibraryStore store)
    {
        _http = http;
        _store = store;
    }

    public async Task LoadAsync()
    {
        if (State != LoadingState.Loading) return;

        try
        {
            var response = await _http.GetAsync("/api/library");

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content
                    .ReadFromJsonAsync<JsonElement>();
                ErrorMessage = err.TryGetProperty("error", out var prop)
                    ? prop.GetString() ?? $"Server returned {(int)response.StatusCode}."
                    : $"Server returned {(int)response.StatusCode}.";
                State = LoadingState.Error;
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<LibraryResponse>();
            if (result is null)
            {
                ErrorMessage = "Server returned an empty response.";
                State = LoadingState.Error;
                return;
            }

            _store.Populate(result.Library);
            WarningMessage = result.Warning;
            State = LoadingState.Loaded;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load step library: {ex.Message}";
            State = LoadingState.Error;
        }
    }
}
```

- [ ] **Step 6: Run the tests — expect PASS**

```powershell
dotnet test tests/Delta.DocView.Tests --filter "LibraryApiClientTests" -v normal
```

Expected: `Passed! - Failed: 0, Passed: 4`

- [ ] **Step 7: Commit**

```powershell
git add src/Delta.DocView.Client/Services/ `
        tests/Delta.DocView.Tests/Services/LibraryApiClientTests.cs
git commit -m "feat: add LibraryApiClient and ClientStepLibraryStore"
```

---

## Task 8: Client — Loading screen, error page, App.razor

**Files:**
- Modify: `src/Delta.DocView.Client/wwwroot/index.html`
- Create: `src/Delta.DocView.Client/Components/LoadingScreen.razor`
- Create: `src/Delta.DocView.Client/Components/StartupErrorPage.razor`
- Modify: `src/Delta.DocView.Client/App.razor`
- Modify: `src/Delta.DocView.Client/Program.cs`
- Create: `tests/Delta.DocView.Tests/Components/LoadingScreenTests.cs`
- Create: `tests/Delta.DocView.Tests/Components/StartupErrorPageTests.cs`

- [ ] **Step 1: Write the failing component tests**

Create `tests/Delta.DocView.Tests/Components/LoadingScreenTests.cs`:

```csharp
using Bunit;
using Delta.DocView.Client.Components;

namespace Delta.DocView.Tests.Components;

public class LoadingScreenTests : TestContext
{
    [Fact]
    public void LoadingScreen_RendersAppName()
    {
        var cut = RenderComponent<LoadingScreen>();

        Assert.Contains("Delta", cut.Markup);
        Assert.Contains("Step Library", cut.Markup);
    }

    [Fact]
    public void LoadingScreen_RendersSpinner()
    {
        var cut = RenderComponent<LoadingScreen>();

        Assert.Contains("loading-spinner", cut.Markup);
    }
}
```

Create `tests/Delta.DocView.Tests/Components/StartupErrorPageTests.cs`:

```csharp
using Bunit;
using Delta.DocView.Client.Components;

namespace Delta.DocView.Tests.Components;

public class StartupErrorPageTests : TestContext
{
    [Fact]
    public void StartupErrorPage_ShowsProvidedErrorMessage()
    {
        var cut = RenderComponent<StartupErrorPage>(p =>
            p.Add(c => c.ErrorMessage, "Library file not found at '/data/step-library.json'."));

        Assert.Contains("Library file not found", cut.Markup);
    }

    [Fact]
    public void StartupErrorPage_ShowsDocviewLibraryPathHint()
    {
        var cut = RenderComponent<StartupErrorPage>(p =>
            p.Add(c => c.ErrorMessage, "some error"));

        Assert.Contains("DOCVIEW_LIBRARY_PATH", cut.Markup);
    }
}
```

- [ ] **Step 2: Run the tests — expect FAIL**

```powershell
dotnet test tests/Delta.DocView.Tests --filter "LoadingScreenTests|StartupErrorPageTests" -v normal
```

Expected: `Failed! - Failed: 4`

- [ ] **Step 3: Create `src/Delta.DocView.Client/Components/LoadingScreen.razor`**

```razor
<div class="loading-screen">
    <div class="loading-brand">
        <svg width="32" height="32" viewBox="0 0 22 22" fill="none">
            <rect x="1.5" y="1.5" width="19" height="19" rx="4.5" fill="#0d9488"/>
            <path d="M6 8h7M6 11h10M6 14h5" stroke="white" stroke-width="1.6" stroke-linecap="round"/>
        </svg>
        <span class="loading-title">Delta · Step Library</span>
    </div>
    <div class="loading-spinner" role="status" aria-label="Loading"></div>
    <p class="loading-sub">Loading step library…</p>
</div>
```

- [ ] **Step 4: Create `src/Delta.DocView.Client/Components/StartupErrorPage.razor`**

```razor
<div class="startup-error">
    <div class="startup-error-icon">⚠</div>
    <h1 class="startup-error-title">Unable to load Delta · Step Library</h1>
    <p class="startup-error-desc">
        The step library could not be loaded. Fix the issue below and restart the container.
    </p>
    <pre class="startup-error-detail">@ErrorMessage</pre>
    <p class="startup-error-hint">
        Set the <code>DOCVIEW_LIBRARY_PATH</code> environment variable to the absolute path
        of a valid <code>step-library.v1.json</code> file.
    </p>
</div>

@code {
    [Parameter, EditorRequired]
    public string ErrorMessage { get; set; } = "";
}
```

- [ ] **Step 5: Run the component tests — expect PASS**

```powershell
dotnet test tests/Delta.DocView.Tests --filter "LoadingScreenTests|StartupErrorPageTests" -v normal
```

Expected: `Passed! - Failed: 0, Passed: 4`

- [ ] **Step 6: Update the static loading screen in `src/Delta.DocView.Client/wwwroot/index.html`**

Replace the generated `<div id="app">` placeholder content (the template puts a spinning SVG here) with a styled loading screen that is visible before WASM boots:

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Delta · Step Library</title>
    <base href="/" />
    <link rel="stylesheet" href="css/app.css" />
    <style>
        .loading-screen {
            display: flex; flex-direction: column; align-items: center;
            justify-content: center; height: 100vh; gap: 16px;
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
            background: #ffffff; color: #1e293b;
        }
        .loading-brand { display: flex; align-items: center; gap: 10px; }
        .loading-title { font-size: 1.1rem; font-weight: 600; }
        .loading-sub { font-size: 0.85rem; color: #64748b; margin: 0; }
        .loading-spinner {
            width: 28px; height: 28px; border: 3px solid #e2e8f0;
            border-top-color: #0d9488; border-radius: 50%;
            animation: spin 0.7s linear infinite;
        }
        @keyframes spin { to { transform: rotate(360deg); } }
    </style>
</head>
<body>
    <div id="app">
        <div class="loading-screen">
            <div class="loading-brand">
                <svg width="28" height="28" viewBox="0 0 22 22" fill="none">
                    <rect x="1.5" y="1.5" width="19" height="19" rx="4.5" fill="#0d9488"/>
                    <path d="M6 8h7M6 11h10M6 14h5" stroke="white" stroke-width="1.6" stroke-linecap="round"/>
                </svg>
                <span class="loading-title">Delta · Step Library</span>
            </div>
            <div class="loading-spinner"></div>
            <p class="loading-sub">Loading…</p>
        </div>
    </div>

    <script src="_framework/blazor.webassembly.js"></script>
</body>
</html>
```

- [ ] **Step 7: Update `src/Delta.DocView.Client/App.razor`**

Replace the generated `App.razor` with:

```razor
@inject LibraryApiClient LibraryClient

@if (LibraryClient.State == LoadingState.Loading)
{
    <LoadingScreen />
}
else if (LibraryClient.State == LoadingState.Error)
{
    <StartupErrorPage ErrorMessage="@(LibraryClient.ErrorMessage ?? "Unknown error.")" />
}
else
{
    @* Main app rendered in US-02 — placeholder for now *@
    <p>Library loaded: @LibraryClient.State</p>
}

@code {
    protected override async Task OnInitializedAsync()
    {
        await LibraryClient.LoadAsync();
    }
}
```

- [ ] **Step 8: Update `src/Delta.DocView.Client/_Imports.razor`**

Add namespace imports so components don't need `@using` directives individually. Open the generated `_Imports.razor` and append:

```razor
@using Delta.DocView.Client.Components
@using Delta.DocView.Client.Services
@using Delta.DocView.Shared
@using Delta.DocView.Shared.Models
```

- [ ] **Step 9: Replace `src/Delta.DocView.Client/Program.cs`**

```csharp
using Delta.DocView.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Delta.DocView.Client.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

builder.Services.AddSingleton<ClientStepLibraryStore>();
builder.Services.AddSingleton<LibraryApiClient>(sp =>
    new LibraryApiClient(
        sp.GetRequiredService<HttpClient>(),
        sp.GetRequiredService<ClientStepLibraryStore>()));

await builder.Build().RunAsync();
```

> Note: `LibraryApiClient` uses `HttpClient` which is registered as `Scoped` in WASM. We construct it via factory overload to satisfy the `Scoped → Singleton` lifetime correctly.

- [ ] **Step 10: Build the full solution**

```powershell
dotnet build Delta.DocView.sln
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 11: Run the full test suite**

```powershell
dotnet test Delta.DocView.sln
```

Expected: all tests pass.

- [ ] **Step 12: Commit**

```powershell
git add src/Delta.DocView.Client/ `
        tests/Delta.DocView.Tests/Components/
git commit -m "feat: add loading screen, startup error page, and App.razor bootstrap"
```

- [ ] **Step 13: Push to GitHub**

```powershell
git push origin master
```

---

## Self-Review

### Spec coverage

| Requirement | Task |
|-------------|------|
| Read from `DOCVIEW_LIBRARY_PATH` env var | Task 6 (Program.cs) |
| Validate against embedded schema | Task 3 (StepLibraryValidator) |
| Missing file → error with path | Task 5 (StartupLoader), Task 8 (StartupErrorPage) |
| Schema invalid → ≤5 errors listed | Task 3 (test), Task 5 (StartupLoader error message) |
| Valid → log steps/version/generatedAt | Task 6 (Program.cs logging) |
| Signature mismatch → warning, still loads | Task 4 + Task 5 (Run_WrongDigest test) |
| Server always starts (no crash on error) | Task 6 (StartupLoader.Run never throws) |
| `GET /api/library` returns `LibraryResponse` | Task 6 (LibraryController) |
| `GET /api/library` returns 503 on error | Task 6 (LibraryController) |
| `GET /health` returns 200/503 | Task 6 (HealthController) |
| WASM loading screen (pre-boot) | Task 8 (index.html) |
| WASM loading screen (post-boot) | Task 8 (LoadingScreen.razor + App.razor) |
| Client error page on 503 | Task 8 (StartupErrorPage.razor + App.razor) |
| `LibraryResponse.Warning` shown as banner | App.razor passes to future warning component; `WarningMessage` stored on `LibraryApiClient` |

### Placeholder scan

No TBDs, TODOs, or "similar to Task N" patterns found.

### Type consistency

- `LibraryApiClient(HttpClient, ClientStepLibraryStore)` — constructor matches Program.cs factory registration.
- `StartupLoader.Run(string, StepLibraryLoader, StepLibraryValidator, StartupError, StepLibraryStore)` — matches all call sites in tests and Program.cs.
- `LibraryResponse(StepLibrary, string?)` — record defined in Shared; used in LibraryController (return) and LibraryApiClient (deserialise).
- `StepLibraryStore.Populate(StepLibrary)` / `ClientStepLibraryStore.Populate(StepLibrary)` — both accept `StepLibrary` from Shared; consistent.
- `LoadingState` enum values `Loading / Loaded / Error` — used consistently in LibraryApiClient, App.razor tests.
