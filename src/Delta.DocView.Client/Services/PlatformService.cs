using Microsoft.JSInterop;

namespace Delta.DocView.Client.Services;

public sealed class PlatformService : IPlatform
{
    private readonly IJSRuntime _js;
    private bool _initialized;

    public bool IsMac { get; private set; }

    public PlatformService(IJSRuntime js) { _js = js; }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        try { IsMac = await _js.InvokeAsync<bool>("docview.platform.isMac"); }
        catch (Exception ex) { Console.WriteLine($"PlatformService.InitializeAsync failed: {ex.Message}"); }
        _initialized = true;
    }

    public string ShortcutLabel(string letter) => IsMac ? $"⌘{letter}" : $"Ctrl+{letter}";
}
