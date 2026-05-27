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
