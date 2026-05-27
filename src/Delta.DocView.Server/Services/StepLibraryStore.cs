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
