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
