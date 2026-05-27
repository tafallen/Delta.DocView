namespace Delta.DocView.Server.Services;

public interface IStartupError
{
    bool HasError { get; }
    string? ErrorMessage { get; }
    bool HasWarning { get; }
    string? WarningMessage { get; }
}
