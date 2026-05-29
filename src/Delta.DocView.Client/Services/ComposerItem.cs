using Delta.DocView.Shared.Models;

namespace Delta.DocView.Client.Services;

public sealed record ComposerItem(Guid Id, Step Step, IReadOnlyList<string> ParamValues)
{
    /// <summary>
    /// Creates a new item pre-filled with each param's Example value (or empty string).
    /// ParamValues count equals the number of ParamToken segments in the step pattern.
    /// </summary>
    public static ComposerItem From(Step step)
    {
        var tokens = PatternTokeniser.Tokenise(step.Pattern);
        var paramCount = tokens.Count(t => t is ParamToken);
        var values = Enumerable.Range(0, paramCount)
            .Select(i => (i < step.Params.Count ? step.Params[i].Example : null) ?? "")
            .ToList();
        return new(Guid.NewGuid(), step, values);
    }
}
