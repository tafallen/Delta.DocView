using System.Text.Json;
using Json.Schema;

namespace Delta.DocView.Server.Services;

public sealed class StepLibraryValidator
{
    private readonly JsonSchema _schema;

    public StepLibraryValidator()
    {
        var asm = typeof(StepLibraryValidator).Assembly;
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
