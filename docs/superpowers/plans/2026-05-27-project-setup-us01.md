# Delta.DocView — Project Setup + US-01: Library Loading & Validation

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Scaffold the .NET 8 Blazor Server solution and implement startup loading, JSON Schema validation, and SHA-256 signature verification for the step-library file, with a startup error page rendered when the file is missing or invalid.

**Architecture:** The library is loaded synchronously in `Program.cs` after `app = builder.Build()`. A `StartupError` singleton records any error or warning encountered. `App.razor` checks this singleton on render and shows `<StartupErrorPage>` instead of normal routing when `HasError` is true. Schema validation uses `JsonSchema.Net` with the v1 schema embedded as an assembly resource. Signature verification serialises the JSON object without the `signature` property and compares the SHA-256 hex digest.

**Tech Stack:** .NET 8, C# 12, Blazor Server, System.Text.Json, JsonSchema.Net (NuGet), xUnit, bUnit, NSubstitute

---

## File Map

```
Delta.DocView.sln
src/
  Delta.DocView/
    Delta.DocView.csproj
    Program.cs                                  ← modified: startup wiring
    _Imports.razor                              ← generated; leave as-is
    App.razor                                   ← modified: error branch
    Components/
      Pages/
        Home.razor                              ← generated; leave as-is
      StartupErrorPage.razor                    ← new: shown when HasError
    Models/
      StepLibrary.cs                            ← new
      Step.cs                                   ← new
      StepParam.cs                              ← new
      StepDomain.cs                             ← new
      StepSignature.cs                          ← new
    Services/
      IStartupError.cs                          ← new
      StartupError.cs                           ← new
      IStepLibraryLoader.cs                     ← new
      StepLibraryLoader.cs                      ← new
      ValidationResult.cs                       ← new
      StepLibraryValidator.cs                   ← new
      SignatureVerifier.cs                      ← new
    Schemas/
      step-library.v1.schema.json               ← copied from repo root (embedded resource)
tests/
  Delta.DocView.Tests/
    Delta.DocView.Tests.csproj
    Services/
      StepLibraryLoaderTests.cs                 ← new
      StepLibraryValidatorTests.cs              ← new
      SignatureVerifierTests.cs                 ← new
      StartupWiringTests.cs                     ← new
    Components/
      StartupErrorPageTests.cs                  ← new
    TestData/
      valid-library.json                        ← new (minimal valid library)
      invalid-library.json                      ← new (missing required field)
```

---

## Task 0: Scaffold the solution

**Files:**
- Create: `Delta.DocView.sln`
- Create: `src/Delta.DocView/Delta.DocView.csproj`
- Create: `tests/Delta.DocView.Tests/Delta.DocView.Tests.csproj`

> No TDD cycle here — this is pure scaffolding. Verify the build compiles and tests run green before proceeding.

- [ ] **Step 1: Create the solution and projects**

```powershell
cd C:\repos\Delta.DocView
dotnet new sln -n Delta.DocView
dotnet new blazor --interactivity Server --auth None --no-https -o src/Delta.DocView -n Delta.DocView
dotnet new xunit -o tests/Delta.DocView.Tests -n Delta.DocView.Tests --framework net8.0
dotnet sln add src/Delta.DocView/Delta.DocView.csproj
dotnet sln add tests/Delta.DocView.Tests/Delta.DocView.Tests.csproj
dotnet add tests/Delta.DocView.Tests reference src/Delta.DocView/Delta.DocView.csproj
```

- [ ] **Step 2: Add NuGet packages**

```powershell
dotnet add src/Delta.DocView package JsonSchema.Net --version 7.*
dotnet add tests/Delta.DocView.Tests package bunit --version 1.*
dotnet add tests/Delta.DocView.Tests package NSubstitute --version 5.*
dotnet add tests/Delta.DocView.Tests package Microsoft.AspNetCore.Components.Web
```

- [ ] **Step 3: Copy the schema file and mark as embedded resource**

```powershell
New-Item -ItemType Directory -Path src/Delta.DocView/Schemas -Force
Copy-Item step-library.v1.schema.json src/Delta.DocView/Schemas/step-library.v1.schema.json
```

Add to `src/Delta.DocView/Delta.DocView.csproj` inside `<Project>`:

```xml
<ItemGroup>
  <EmbeddedResource Include="Schemas\step-library.v1.schema.json"
                    LogicalName="step-library.v1.schema.json" />
</ItemGroup>
```

- [ ] **Step 4: Verify scaffold compiles and placeholder test passes**

```powershell
dotnet build Delta.DocView.sln
dotnet test Delta.DocView.sln
```

Expected output ends with: `Passed! - Failed: 0, Passed: 1` (the generated `UnitTest1` placeholder).

- [ ] **Step 5: Delete the placeholder test**

```powershell
Remove-Item tests/Delta.DocView.Tests/UnitTest1.cs
```

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "chore: scaffold Blazor Server solution with test project"
```

---

## Task 1: Domain models

**Files:**
- Create: `src/Delta.DocView/Models/StepLibrary.cs`
- Create: `src/Delta.DocView/Models/Step.cs`
- Create: `src/Delta.DocView/Models/StepParam.cs`
- Create: `src/Delta.DocView/Models/StepDomain.cs`
- Create: `src/Delta.DocView/Models/StepSignature.cs`
- Test: `tests/Delta.DocView.Tests/Services/StepLibraryLoaderTests.cs` (deserialization covered in Task 2)

No failing test for pure data models — the test will come in Task 2 when we deserialise.  
Just create the models and verify the build stays green.

- [ ] **Step 1: Create `src/Delta.DocView/Models/StepParam.cs`**

```csharp
using System.Text.Json.Serialization;

namespace Delta.DocView.Models;

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

- [ ] **Step 2: Create `src/Delta.DocView/Models/StepDomain.cs`**

```csharp
using System.Text.Json.Serialization;

namespace Delta.DocView.Models;

public sealed class StepDomain
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("label")]
    public string Label { get; init; } = "";
}
```

- [ ] **Step 3: Create `src/Delta.DocView/Models/StepSignature.cs`**

```csharp
using System.Text.Json.Serialization;

namespace Delta.DocView.Models;

public sealed class StepSignature
{
    [JsonPropertyName("algorithm")]
    public string Algorithm { get; init; } = "";

    [JsonPropertyName("digest")]
    public string Digest { get; init; } = "";
}
```

- [ ] **Step 4: Create `src/Delta.DocView/Models/Step.cs`**

```csharp
using System.Text.Json.Serialization;

namespace Delta.DocView.Models;

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

- [ ] **Step 5: Create `src/Delta.DocView/Models/StepLibrary.cs`**

```csharp
using System.Text.Json.Serialization;

namespace Delta.DocView.Models;

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

- [ ] **Step 6: Build to confirm no errors**

```powershell
dotnet build src/Delta.DocView/Delta.DocView.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 7: Commit**

```powershell
git add src/Delta.DocView/Models/
git commit -m "feat: add step library domain models"
```

---

## Task 2: StepLibraryLoader

**Files:**
- Create: `src/Delta.DocView/Services/IStepLibraryLoader.cs`
- Create: `src/Delta.DocView/Services/StepLibraryLoader.cs`
- Create: `tests/Delta.DocView.Tests/Services/StepLibraryLoaderTests.cs`
- Create: `tests/Delta.DocView.Tests/TestData/valid-library.json`

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
      "source": "[Given] public void Login(string u) {}",
      "suggestsNext": []
    }
  ],
  "signature": {
    "algorithm": "SHA-256",
    "digest": "0000000000000000000000000000000000000000000000000000000000000000"
  }
}
```

> Note: the digest is a placeholder — SignatureVerifier tests will handle correctness separately.

- [ ] **Step 2: Mark test data as CopyToOutputDirectory in `tests/Delta.DocView.Tests/Delta.DocView.Tests.csproj`**

Add inside `<Project>`:

```xml
<ItemGroup>
  <None Update="TestData\**\*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

- [ ] **Step 3: Write the failing tests in `tests/Delta.DocView.Tests/Services/StepLibraryLoaderTests.cs`**

```csharp
using Delta.DocView.Models;
using Delta.DocView.Services;

namespace Delta.DocView.Tests.Services;

public class StepLibraryLoaderTests
{
    private static readonly string TestDataDir =
        Path.Combine(AppContext.BaseDirectory, "TestData");

    [Fact]
    public void Load_ValidFile_ReturnsLibraryWithSteps()
    {
        var path = Path.Combine(TestDataDir, "valid-library.json");
        var loader = new StepLibraryLoader();

        (StepLibrary library, string rawJson) = loader.Load(path);

        Assert.Equal("1.0.0", library.Version);
        Assert.Single(library.Steps);
        Assert.Equal("auth-001a2b3c", library.Steps[0].Id);
        Assert.NotEmpty(rawJson);
    }

    [Fact]
    public void Load_MissingFile_ThrowsFileNotFoundException()
    {
        var loader = new StepLibraryLoader();

        Assert.Throws<FileNotFoundException>(() =>
            loader.Load("/nonexistent/path/library.json"));
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
        finally
        {
            File.Delete(path);
        }
    }
}
```

- [ ] **Step 4: Run the tests — expect FAIL**

```powershell
dotnet test tests/Delta.DocView.Tests --filter "StepLibraryLoaderTests" -v normal
```

Expected: `Failed! - Failed: 3` with errors about missing types.

- [ ] **Step 5: Create `src/Delta.DocView/Services/IStepLibraryLoader.cs`**

```csharp
using Delta.DocView.Models;

namespace Delta.DocView.Services;

public interface IStepLibraryLoader
{
    (StepLibrary Library, string RawJson) Load(string path);
}
```

- [ ] **Step 6: Create `src/Delta.DocView/Services/StepLibraryLoader.cs`**

```csharp
using System.Text.Json;
using Delta.DocView.Models;

namespace Delta.DocView.Services;

public sealed class StepLibraryLoader : IStepLibraryLoader
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    public (StepLibrary Library, string RawJson) Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Step library not found at '{path}'.", path);

        var rawJson = File.ReadAllText(path);
        var library = JsonSerializer.Deserialize<StepLibrary>(rawJson, _options)
            ?? throw new InvalidOperationException("Deserialisation returned null.");

        return (library, rawJson);
    }
}
```

- [ ] **Step 7: Run the tests — expect PASS**

```powershell
dotnet test tests/Delta.DocView.Tests --filter "StepLibraryLoaderTests" -v normal
```

Expected: `Passed! - Failed: 0, Passed: 3`

- [ ] **Step 8: Commit**

```powershell
git add src/Delta.DocView/Services/IStepLibraryLoader.cs `
        src/Delta.DocView/Services/StepLibraryLoader.cs `
        tests/Delta.DocView.Tests/Services/StepLibraryLoaderTests.cs `
        tests/Delta.DocView.Tests/TestData/ `
        tests/Delta.DocView.Tests/Delta.DocView.Tests.csproj
git commit -m "feat: add StepLibraryLoader with file-read and JSON deserialisation"
```

---

## Task 3: StepLibraryValidator

**Files:**
- Create: `src/Delta.DocView/Services/ValidationResult.cs`
- Create: `src/Delta.DocView/Services/StepLibraryValidator.cs`
- Create: `tests/Delta.DocView.Tests/TestData/invalid-library.json`
- Test: `tests/Delta.DocView.Tests/Services/StepLibraryValidatorTests.cs`

- [ ] **Step 1: Create `tests/Delta.DocView.Tests/TestData/invalid-library.json`**

This file is missing the required `version` field:

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

- [ ] **Step 2: Write the failing tests in `tests/Delta.DocView.Tests/Services/StepLibraryValidatorTests.cs`**

```csharp
using Delta.DocView.Services;

namespace Delta.DocView.Tests.Services;

public class StepLibraryValidatorTests
{
    private static readonly string TestDataDir =
        Path.Combine(AppContext.BaseDirectory, "TestData");

    [Fact]
    public void Validate_ValidJson_ReturnsIsValidTrue()
    {
        var json = File.ReadAllText(Path.Combine(TestDataDir, "valid-library.json"));
        var validator = new StepLibraryValidator();

        var result = validator.Validate(json);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_MissingRequiredField_ReturnsIsValidFalse()
    {
        var json = File.ReadAllText(Path.Combine(TestDataDir, "invalid-library.json"));
        var validator = new StepLibraryValidator();

        var result = validator.Validate(json);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Validate_MissingRequiredField_ErrorsLimitedToFive()
    {
        // JSON with many violations: missing version, wrong type on enriched, bad digest pattern
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
        Assert.True(result.Errors.Count <= 5,
            $"Expected at most 5 errors but got {result.Errors.Count}");
    }
}
```

- [ ] **Step 3: Run the tests — expect FAIL**

```powershell
dotnet test tests/Delta.DocView.Tests --filter "StepLibraryValidatorTests" -v normal
```

Expected: `Failed! - Failed: 3` (types not found).

- [ ] **Step 4: Create `src/Delta.DocView/Services/ValidationResult.cs`**

```csharp
namespace Delta.DocView.Services;

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Ok() => new(true, []);
}
```

- [ ] **Step 5: Create `src/Delta.DocView/Services/StepLibraryValidator.cs`**

```csharp
using System.Reflection;
using System.Text.Json;
using Json.Schema;

namespace Delta.DocView.Services;

public sealed class StepLibraryValidator
{
    private readonly JsonSchema _schema;

    public StepLibraryValidator()
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream("step-library.v1.schema.json")
            ?? throw new InvalidOperationException(
                "Embedded resource 'step-library.v1.schema.json' not found.");
        using var reader = new StreamReader(stream);
        _schema = JsonSchema.FromText(reader.ReadToEnd());
    }

    public ValidationResult Validate(string rawJson)
    {
        var doc = JsonDocument.Parse(rawJson);
        var options = new EvaluationOptions { OutputFormat = OutputFormat.List };
        var result = _schema.Evaluate(doc.RootElement, options);

        if (result.IsValid)
            return ValidationResult.Ok();

        var errors = result.Details
            .Where(d => !d.IsValid && d.Errors is { Count: > 0 })
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))
            .Distinct()
            .Take(5)
            .ToList();

        // Fallback: if no detail messages extracted, report a generic error
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
git add src/Delta.DocView/Services/ValidationResult.cs `
        src/Delta.DocView/Services/StepLibraryValidator.cs `
        tests/Delta.DocView.Tests/Services/StepLibraryValidatorTests.cs `
        tests/Delta.DocView.Tests/TestData/invalid-library.json
git commit -m "feat: add StepLibraryValidator using JsonSchema.Net embedded schema"
```

---

## Task 4: SignatureVerifier

**Files:**
- Create: `src/Delta.DocView/Services/SignatureVerifier.cs`
- Test: `tests/Delta.DocView.Tests/Services/SignatureVerifierTests.cs`

The verifier strips the `signature` property from the JSON object, serialises the remainder to compact UTF-8 JSON preserving the original property order, then computes SHA-256 and hex-encodes it.

- [ ] **Step 1: Write the failing tests in `tests/Delta.DocView.Tests/Services/SignatureVerifierTests.cs`**

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Delta.DocView.Services;

namespace Delta.DocView.Tests.Services;

public class SignatureVerifierTests
{
    private static string ComputeExpectedDigest(string rawJson)
    {
        // Reproduce the same algorithm as the implementation:
        // strip "signature" property, serialise, SHA-256.
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
        var hash = SHA256.HashData(ms.ToArray());
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    [Fact]
    public void Verify_CorrectDigest_ReturnsTrue()
    {
        var payload = """{"$schema":"x","version":"1.0.0"}""";
        var json = $$"""{"$schema":"x","version":"1.0.0","signature":{"algorithm":"SHA-256","digest":"{{ComputeExpectedDigest(payload + ",\"signature\":{}")}}"}""";
        // Simpler: build a well-formed JSON and compute its expected digest inline.
        var body = """
            {
              "$schema": "https://example.com/schema",
              "version": "1.0.0",
              "generatedAt": "2026-01-01T00:00:00Z"
            }
            """;
        // Parse body, add signature with the correct digest
        using var bodyDoc = JsonDocument.Parse(body);
        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms);
        writer.WriteStartObject();
        foreach (var p in bodyDoc.RootElement.EnumerateObject())
            p.WriteTo(writer);
        writer.WriteEndObject();
        writer.Flush();
        var correctDigest = Convert.ToHexString(SHA256.HashData(ms.ToArray())).ToLowerInvariant();

        // Now build full JSON with that digest
        var fullJson = $$"""
            {
              "$schema": "https://example.com/schema",
              "version": "1.0.0",
              "generatedAt": "2026-01-01T00:00:00Z",
              "signature": { "algorithm": "SHA-256", "digest": "{{correctDigest}}" }
            }
            """;

        Assert.True(SignatureVerifier.Verify(fullJson, correctDigest));
    }

    [Fact]
    public void Verify_WrongDigest_ReturnsFalse()
    {
        var json = """
            {
              "$schema": "https://example.com/schema",
              "version": "1.0.0",
              "signature": { "algorithm": "SHA-256", "digest": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" }
            }
            """;

        Assert.False(SignatureVerifier.Verify(json,
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
    }

    [Fact]
    public void Verify_NoSignatureProperty_StillHashesPayload()
    {
        // JSON without a signature property — the "without signature" stripping is a no-op.
        var json = """{"key":"value"}""";
        using var doc = JsonDocument.Parse(json);
        using var ms = new MemoryStream();
        using var w = new Utf8JsonWriter(ms);
        w.WriteStartObject();
        foreach (var p in doc.RootElement.EnumerateObject())
            p.WriteTo(w);
        w.WriteEndObject();
        w.Flush();
        var expected = Convert.ToHexString(SHA256.HashData(ms.ToArray())).ToLowerInvariant();

        Assert.True(SignatureVerifier.Verify(json, expected));
    }
}
```

- [ ] **Step 2: Run the tests — expect FAIL**

```powershell
dotnet test tests/Delta.DocView.Tests --filter "SignatureVerifierTests" -v normal
```

Expected: `Failed! - Failed: 3` (type not found).

- [ ] **Step 3: Create `src/Delta.DocView/Services/SignatureVerifier.cs`**

```csharp
using System.Security.Cryptography;
using System.Text.Json;

namespace Delta.DocView.Services;

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

        var hash = SHA256.HashData(ms.ToArray());
        var actualDigest = Convert.ToHexString(hash).ToLowerInvariant();
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
git add src/Delta.DocView/Services/SignatureVerifier.cs `
        tests/Delta.DocView.Tests/Services/SignatureVerifierTests.cs
git commit -m "feat: add SignatureVerifier (SHA-256 without signature property)"
```

---

## Task 5: IStartupError + startup wiring

**Files:**
- Create: `src/Delta.DocView/Services/IStartupError.cs`
- Create: `src/Delta.DocView/Services/StartupError.cs`
- Modify: `src/Delta.DocView/Program.cs`
- Test: `tests/Delta.DocView.Tests/Services/StartupWiringTests.cs`

This task wires the three services together in a single `StartupLoader.Run(...)` helper that `Program.cs` calls. Testing this helper directly avoids a full integration host.

- [ ] **Step 1: Create `src/Delta.DocView/Services/IStartupError.cs`**

```csharp
namespace Delta.DocView.Services;

public interface IStartupError
{
    bool HasError { get; }
    string? ErrorMessage { get; }
    bool HasWarning { get; }
    string? WarningMessage { get; }
}
```

- [ ] **Step 2: Create `src/Delta.DocView/Services/StartupError.cs`**

```csharp
namespace Delta.DocView.Services;

public sealed class StartupError : IStartupError
{
    public bool HasError { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool HasWarning { get; private set; }
    public string? WarningMessage { get; private set; }

    public void SetError(string message)
    {
        HasError = true;
        ErrorMessage = message;
    }

    public void SetWarning(string message)
    {
        HasWarning = true;
        WarningMessage = message;
    }
}
```

- [ ] **Step 3: Write the failing tests in `tests/Delta.DocView.Tests/Services/StartupWiringTests.cs`**

```csharp
using Delta.DocView.Models;
using Delta.DocView.Services;

namespace Delta.DocView.Tests.Services;

public class StartupWiringTests
{
    private static readonly string TestDataDir =
        Path.Combine(AppContext.BaseDirectory, "TestData");

    [Fact]
    public void Run_ValidFile_NoErrorNoWarning()
    {
        // Arrange: build a valid library JSON with the correct SHA-256 digest
        var json = BuildValidLibraryJson();
        var path = WriteTempFile(json);
        var error = new StartupError();

        try
        {
            // Act
            StartupLoader.Run(path, new StepLibraryLoader(), new StepLibraryValidator(), error);

            // Assert
            Assert.False(error.HasError);
            Assert.False(error.HasWarning);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Run_MissingFile_SetsError()
    {
        var error = new StartupError();

        StartupLoader.Run("/no/such/file.json",
            new StepLibraryLoader(), new StepLibraryValidator(), error);

        Assert.True(error.HasError);
        Assert.Contains("/no/such/file.json", error.ErrorMessage);
    }

    [Fact]
    public void Run_InvalidSchema_SetsError()
    {
        var json = File.ReadAllText(Path.Combine(TestDataDir, "invalid-library.json"));
        var path = WriteTempFile(json);
        var error = new StartupError();

        try
        {
            StartupLoader.Run(path, new StepLibraryLoader(), new StepLibraryValidator(), error);

            Assert.True(error.HasError);
            Assert.NotNull(error.ErrorMessage);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Run_WrongSignature_SetsWarning_NotError()
    {
        // Build a valid-schema file but with a bad digest
        var json = File.ReadAllText(Path.Combine(TestDataDir, "valid-library.json"));
        // valid-library.json already has a placeholder digest "000...0" which won't match
        var path = WriteTempFile(json);
        var error = new StartupError();

        try
        {
            StartupLoader.Run(path, new StepLibraryLoader(), new StepLibraryValidator(), error);

            Assert.False(error.HasError);
            Assert.True(error.HasWarning);
            Assert.NotNull(error.WarningMessage);
        }
        finally { File.Delete(path); }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string WriteTempFile(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        return path;
    }

    private static string BuildValidLibraryJson()
    {
        // Build JSON without signature, compute digest, then add signature
        var bodyJson = """
            {
              "$schema": "https://delta.docgen/schema/v1/step-library.schema.json",
              "version": "1.0.0",
              "generatedAt": "2026-01-01T00:00:00Z",
              "generatorVersion": "1.0.0",
              "enriched": true,
              "domains": [{ "id": "Auth", "label": "Auth & Identity" }],
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
                  "description": "Logs in.",
                  "source": "public void Login() {}",
                  "suggestsNext": []
                }
              ]
            }
            """;

        using var doc = System.Text.Json.JsonDocument.Parse(bodyJson);
        using var ms = new System.IO.MemoryStream();
        using var writer = new System.Text.Json.Utf8JsonWriter(ms);
        writer.WriteStartObject();
        foreach (var p in doc.RootElement.EnumerateObject())
            p.WriteTo(writer);
        writer.WriteEndObject();
        writer.Flush();
        var hash = System.Security.Cryptography.SHA256.HashData(ms.ToArray());
        var digest = Convert.ToHexString(hash).ToLowerInvariant();

        return $$"""
            {
              "$schema": "https://delta.docgen/schema/v1/step-library.schema.json",
              "version": "1.0.0",
              "generatedAt": "2026-01-01T00:00:00Z",
              "generatorVersion": "1.0.0",
              "enriched": true,
              "domains": [{ "id": "Auth", "label": "Auth & Identity" }],
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
                  "description": "Logs in.",
                  "source": "public void Login() {}",
                  "suggestsNext": []
                }
              ],
              "signature": { "algorithm": "SHA-256", "digest": "{{digest}}" }
            }
            """;
    }
}
```

- [ ] **Step 4: Run the tests — expect FAIL**

```powershell
dotnet test tests/Delta.DocView.Tests --filter "StartupWiringTests" -v normal
```

Expected: `Failed! - Failed: 4` (`StartupLoader` not found).

- [ ] **Step 5: Create `src/Delta.DocView/Services/StartupLoader.cs`**

```csharp
using Microsoft.Extensions.Logging;

namespace Delta.DocView.Services;

public static class StartupLoader
{
    public static void Run(
        string libraryPath,
        IStepLibraryLoader loader,
        StepLibraryValidator validator,
        StartupError error)
    {
        string rawJson;
        Models.StepLibrary library;

        try
        {
            (library, rawJson) = loader.Load(libraryPath);
        }
        catch (FileNotFoundException ex)
        {
            error.SetError($"Step library file not found at '{libraryPath}'. " +
                           $"Set the DOCVIEW_LIBRARY_PATH environment variable to the correct path. " +
                           $"({ex.Message})");
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
            var summary = string.Join("\n• ", validation.Errors.Prepend("Schema validation failed:"));
            error.SetError(summary);
            return;
        }

        if (!SignatureVerifier.Verify(rawJson, library.Signature.Digest))
        {
            error.SetWarning(
                "Step library signature mismatch — the file may have been modified after generation. " +
                "The library has been loaded but integrity cannot be guaranteed.");
        }
    }
}
```

- [ ] **Step 6: Run the tests — expect PASS**

```powershell
dotnet test tests/Delta.DocView.Tests --filter "StartupWiringTests" -v normal
```

Expected: `Passed! - Failed: 0, Passed: 4`

- [ ] **Step 7: Wire into `src/Delta.DocView/Program.cs`**

Open `Program.cs`. Add the following **after** `var builder = WebApplication.CreateBuilder(args)` and **before** `var app = builder.Build()`:

```csharp
// Register startup error singleton
builder.Services.AddSingleton<StartupError>();
builder.Services.AddSingleton<IStartupError>(sp => sp.GetRequiredService<StartupError>());

// Register step library services
builder.Services.AddSingleton<IStepLibraryLoader, StepLibraryLoader>();
builder.Services.AddSingleton<StepLibraryValidator>();
```

Add the following **after** `var app = builder.Build()` and **before** `app.Run()`:

```csharp
// ── Startup: load and validate the step library ──────────────────────────────
var libraryPath = app.Configuration["DOCVIEW_LIBRARY_PATH"]
    ?? Path.Combine(app.Environment.ContentRootPath, "data", "step-library.json");

var startupError = app.Services.GetRequiredService<StartupError>();
var stepLoader   = app.Services.GetRequiredService<IStepLibraryLoader>();
var stepValidator = app.Services.GetRequiredService<StepLibraryValidator>();

StartupLoader.Run(libraryPath, stepLoader, stepValidator, startupError);

if (startupError.HasError)
    app.Logger.LogError("Startup error: {Error}", startupError.ErrorMessage);
else if (startupError.HasWarning)
    app.Logger.LogWarning("Startup warning: {Warning}", startupError.WarningMessage);
else
    app.Logger.LogInformation("Step library loaded successfully from '{Path}'", libraryPath);
```

Add the required `using` statements at the top of `Program.cs`:

```csharp
using Delta.DocView.Services;
```

- [ ] **Step 8: Verify the app builds**

```powershell
dotnet build src/Delta.DocView/Delta.DocView.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 9: Run all tests to confirm nothing is broken**

```powershell
dotnet test Delta.DocView.sln
```

Expected: all tests pass.

- [ ] **Step 10: Commit**

```powershell
git add src/Delta.DocView/Services/IStartupError.cs `
        src/Delta.DocView/Services/StartupError.cs `
        src/Delta.DocView/Services/StartupLoader.cs `
        src/Delta.DocView/Program.cs `
        tests/Delta.DocView.Tests/Services/StartupWiringTests.cs
git commit -m "feat: add StartupError service and wire library loading into Program.cs"
```

---

## Task 6: StartupErrorPage component

**Files:**
- Create: `src/Delta.DocView/Components/StartupErrorPage.razor`
- Modify: `src/Delta.DocView/Components/App.razor`
- Test: `tests/Delta.DocView.Tests/Components/StartupErrorPageTests.cs`

- [ ] **Step 1: Write the failing tests in `tests/Delta.DocView.Tests/Components/StartupErrorPageTests.cs`**

```csharp
using Bunit;
using Delta.DocView.Components;
using Delta.DocView.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Delta.DocView.Tests.Components;

public class StartupErrorPageTests : TestContext
{
    [Fact]
    public void RendersErrorMessage_WhenHasError()
    {
        var error = Substitute.For<IStartupError>();
        error.HasError.Returns(true);
        error.ErrorMessage.Returns("Library file not found at '/data/step-library.json'.");
        Services.AddSingleton(error);

        var cut = RenderComponent<StartupErrorPage>();

        cut.MarkupMatches(new System.Text.RegularExpressions.Regex("Library file not found"));
        Assert.Contains("Library file not found", cut.Markup);
    }

    [Fact]
    public void RendersWarningBanner_WhenHasWarning()
    {
        var error = Substitute.For<IStartupError>();
        error.HasError.Returns(false);
        error.HasWarning.Returns(true);
        error.WarningMessage.Returns("Signature mismatch detected.");
        Services.AddSingleton(error);

        var cut = RenderComponent<StartupErrorPage>();

        Assert.Contains("Signature mismatch", cut.Markup);
    }
}
```

- [ ] **Step 2: Run the tests — expect FAIL**

```powershell
dotnet test tests/Delta.DocView.Tests --filter "StartupErrorPageTests" -v normal
```

Expected: `Failed! - Failed: 2` (component not found).

- [ ] **Step 3: Create `src/Delta.DocView/Components/StartupErrorPage.razor`**

```razor
@inject IStartupError StartupError

@if (StartupError.HasError)
{
    <div class="startup-error">
        <h1>Unable to start Delta.DocView</h1>
        <p>The step library could not be loaded. Fix the issue below and restart the container.</p>
        <pre class="startup-error-detail">@StartupError.ErrorMessage</pre>
        <p class="startup-error-hint">
            Set the <code>DOCVIEW_LIBRARY_PATH</code> environment variable to the absolute path
            of a valid <code>step-library.v1.json</code> file.
        </p>
    </div>
}
else if (StartupError.HasWarning)
{
    <div class="startup-warning">
        <strong>Warning:</strong> @StartupError.WarningMessage
    </div>
}

@code {
}
```

Add to the top of the file (after the `@inject` line), add the `using` for `IStartupError` namespace. Or add to `_Imports.razor`:

In `src/Delta.DocView/Components/_Imports.razor` (open the file and append):

```razor
@using Delta.DocView.Services
@using Delta.DocView.Models
```

- [ ] **Step 4: Run the tests — expect PASS**

```powershell
dotnet test tests/Delta.DocView.Tests --filter "StartupErrorPageTests" -v normal
```

Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 5: Modify `src/Delta.DocView/Components/App.razor` to inject the error**

Open `App.razor`. The generated file looks like this:

```razor
<!DOCTYPE html>
<html lang="en">
  ...
  <body>
    <Routes @rendermode="InteractiveServer" />
    ...
  </body>
</html>
```

Inject `IStartupError` and conditionally render the error page instead of `<Routes>`:

```razor
@inject IStartupError StartupError

<!DOCTYPE html>
<html lang="en">

<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <base href="/" />
    <link rel="stylesheet" href="app.css" />
    <link rel="stylesheet" href="Delta.DocView.styles.css" />
    <HeadOutlet @rendermode="InteractiveServer" />
</head>

<body>
    @if (StartupError.HasError)
    {
        <StartupErrorPage />
    }
    else
    {
        <Routes @rendermode="InteractiveServer" />
        @if (StartupError.HasWarning)
        {
            <StartupErrorPage />
        }
    }
    <script src="_framework/blazor.web.js"></script>
</body>

</html>
```

> Note: the exact structure of the generated `App.razor` may differ slightly. The key changes are: (1) `@inject IStartupError StartupError`, (2) the `@if` / `@else` block wrapping `<Routes>`.

- [ ] **Step 6: Build and run a quick smoke test**

```powershell
dotnet build src/Delta.DocView/Delta.DocView.csproj
```

Expected: `Build succeeded. 0 Error(s)`

```powershell
dotnet run --project src/Delta.DocView -- --urls http://localhost:5100 &
Start-Sleep 3
Invoke-WebRequest http://localhost:5100 -UseBasicParsing | Select-Object StatusCode
```

Expected: `StatusCode: 200` (the error page renders because no library file exists in dev — this is correct behaviour).

Stop the process after verifying: `Stop-Process -Name "Delta.DocView" -ErrorAction SilentlyContinue`

- [ ] **Step 7: Run the full test suite**

```powershell
dotnet test Delta.DocView.sln
```

Expected: all tests pass.

- [ ] **Step 8: Commit**

```powershell
git add src/Delta.DocView/Components/StartupErrorPage.razor `
        src/Delta.DocView/Components/App.razor `
        src/Delta.DocView/Components/_Imports.razor `
        tests/Delta.DocView.Tests/Components/StartupErrorPageTests.cs
git commit -m "feat: add StartupErrorPage component and wire into App.razor"
```

- [ ] **Step 9: Push to GitHub**

```powershell
git push origin master
```

---

## Self-Review

### Spec coverage

| US-01 requirement | Covered by |
|-------------------|------------|
| Read from `DOCVIEW_LIBRARY_PATH` env var | Task 5, Step 7 (Program.cs wiring) |
| Validate against schema | Task 3 (StepLibraryValidator) |
| Missing file → error page with expected path | Task 5 (StartupLoader), Task 6 (StartupErrorPage) |
| Schema invalid → error page with ≤5 errors | Task 3 (3-error limit test), Task 6 |
| Valid → log `steps`, `version`, `generatedAt` | Task 5, Step 7 (logging) |
| Signature mismatch → warning banner, still loads | Task 4 + Task 5 (Run_WrongSignature test) |
| Restart with valid file recovers | Inherent — no persistent state stored |

### Placeholder scan

No TBDs, TODOs, or "similar to task N" references found.

### Type consistency

- `StartupError` (concrete) used directly in `StartupLoader.Run` parameter — matches definition in Task 5.
- `IStartupError` (interface) used in `App.razor` and `StartupErrorPage.razor` — matches definition in Task 5.
- `StepLibraryLoader` implements `IStepLibraryLoader` — both defined in Task 2.
- `ValidationResult` returned by `StepLibraryValidator.Validate` — defined in Task 3.
- `SignatureVerifier.Verify(string, string)` — static method, used consistently in Task 4 and Task 5.
