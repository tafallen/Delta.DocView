using Delta.DocView.Shared.Models;

namespace Delta.DocView.Server.Services;

public interface IStepLibraryStore
{
    bool IsLoaded { get; }
    StepLibrary? Library { get; }
}
