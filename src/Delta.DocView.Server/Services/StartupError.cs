namespace Delta.DocView.Server.Services;

public sealed class StartupError : IStartupError
{
    private enum LoadState { None, Warning, Error }
    private LoadState _state;
    private string? _message;

    public bool HasError   => _state == LoadState.Error;
    public bool HasWarning => _state == LoadState.Warning;
    public string? ErrorMessage   => _state == LoadState.Error   ? _message : null;
    public string? WarningMessage => _state == LoadState.Warning ? _message : null;

    public void SetError(string message)
    {
        _state   = LoadState.Error;
        _message = message;
    }

    public void SetWarning(string message)
    {
        if (_state == LoadState.Error) return; // errors win; ignore subsequent warnings
        _state   = LoadState.Warning;
        _message = message;
    }
}
