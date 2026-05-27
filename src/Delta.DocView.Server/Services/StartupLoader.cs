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
        Delta.DocView.Shared.Models.StepLibrary library;

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

        if (!SignatureVerifier.Verify(rawJson))
        {
            error.SetWarning(
                "Step library signature mismatch — the file may have been modified " +
                "after generation. The library is loaded but integrity cannot be guaranteed.");
        }

        store.Populate(library);
    }
}
