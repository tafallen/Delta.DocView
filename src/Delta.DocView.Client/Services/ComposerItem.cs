using Delta.DocView.Shared.Models;

namespace Delta.DocView.Client.Services;

public sealed record ComposerItem(Guid Id, Step Step)
{
    public static ComposerItem From(Step step) => new(Guid.NewGuid(), step);
}
