using System.Text.Json;
using Microsoft.JSInterop;

namespace Delta.DocView.Client.Services;

/// <summary>
/// Scoped store for the US-11 appearance tweaks (dark mode, accent, density,
/// row emphasis, source-section default), backed by the browser's localStorage
/// via the <c>docview.tweaks</c> JS helpers (storage key owned by the JS side).
/// <para>
/// Mirrors the <see cref="LocalStorageFavouritesStore"/> contract: the in-memory
/// state is authoritative for the session; <see cref="InitializeAsync"/> hydrates
/// on boot (falling back to the OS <c>prefers-color-scheme</c> for dark when nothing
/// is stored), and setters fire best-effort writes back to localStorage plus a live
/// <c>applyRoot</c> that sets the <c>data-*</c> attributes on the document element.
/// </para>
/// <para>
/// <b>Persistence contract (mirrored in <c>docview.js</c>).</b>
/// <list type="bullet">
/// <item><description>Storage key: <c>docview.tweaks.v1</c></description></item>
/// <item><description>Payload shape: JSON <c>{ dark, followOs, accent, density, rowEmphasis, source }</c>
/// with enum values as lowercase strings.</description></item>
/// <item><description>Malformed JSON / JS errors: treated as defaults, logged.</description></item>
/// <item><description>Migration policy: changing the payload shape requires bumping the
/// key suffix (e.g. <c>.v2</c>) in both this class's JS helpers and <c>docview.js</c>.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Follow OS contract.</b> When <see cref="FollowOs"/> is true a JS <c>matchMedia</c> listener
/// is active; OS colour-scheme changes call back into <see cref="OnOsColorSchemeChanged"/>. The
/// <see cref="DotNetObjectReference{T}"/> used for the callback is created lazily and disposed in
/// <see cref="Dispose"/>.
/// </para>
/// </summary>
/// <remarks>
/// <b>Disposal contract.</b> <see cref="Changed"/> is a plain multicast event; subscribers
/// (typically components) MUST unsubscribe (e.g. in <c>Dispose</c>) to avoid leaking, since
/// this store is scoped and outlives transient components.
/// </remarks>
public sealed class TweaksStore : IDisposable
{
    private readonly IJSRuntime _js;
    private bool _initialized;
    private DotNetObjectReference<TweaksStore>? _dotNetRef;

    public TweaksStore(IJSRuntime js)
    {
        _js = js;
    }

    public bool Dark       { get; private set; }
    public bool FollowOs   { get; private set; }
    public AccentOption        Accent        { get; private set; } = AccentOption.Orange;
    public DensityOption       Density       { get; private set; } = DensityOption.Comfortable;
    public RowEmphasisOption   RowEmphasis   { get; private set; } = RowEmphasisOption.Pattern;
    public SourceDefaultOption SourceDefault { get; private set; } = SourceDefaultOption.Collapsed;

    public event Action? Changed;

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            var json = await _js.InvokeAsync<string>("docview.tweaks.read");
            Dto? dto = null;
            if (!string.IsNullOrEmpty(json))
            {
                dto = JsonSerializer.Deserialize<Dto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }

            if (dto?.followOs is true)
            {
                FollowOs = true;
                Dark = await _js.InvokeAsync<bool>("docview.prefersDark");
                _dotNetRef ??= DotNetObjectReference.Create(this);
                _ = WatchOsAsync();
            }
            else if (dto?.dark is bool dark)
            {
                Dark = dark;
            }
            else
            {
                Dark = await _js.InvokeAsync<bool>("docview.prefersDark");
            }

            if (Enum.TryParse<AccentOption>(dto?.accent, ignoreCase: true, out var accent))
                Accent = accent;
            if (Enum.TryParse<DensityOption>(dto?.density, ignoreCase: true, out var density))
                Density = density;
            if (Enum.TryParse<RowEmphasisOption>(dto?.rowEmphasis, ignoreCase: true, out var rowEmphasis))
                RowEmphasis = rowEmphasis;
            if (Enum.TryParse<SourceDefaultOption>(dto?.source, ignoreCase: true, out var source))
                SourceDefault = source;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"TweaksStore.InitializeAsync failed: {ex.Message}");
        }
        finally
        {
            _initialized = true;
            await ApplyRootAsync();
        }
    }

    public void SetDark(bool value)
    {
        if (value == Dark) return;
        Dark = value;
        _ = PersistAsync();
        _ = ApplyRootAsync();
        Changed?.Invoke();
    }

    public void SetFollowOs(bool value)
    {
        if (value == FollowOs) return;
        FollowOs = value;
        if (value)
        {
            _dotNetRef ??= DotNetObjectReference.Create(this);
            _ = WatchOsAsync();
            _ = SyncOsDarkAsync();   // immediately read current OS preference
        }
        else
        {
            _ = UnwatchOsAsync();
        }
        _ = PersistAsync();
        Changed?.Invoke();
    }

    public void SetAccent(AccentOption value)
    {
        if (value == Accent) return;
        Accent = value;
        _ = PersistAsync();
        _ = ApplyRootAsync();
        Changed?.Invoke();
    }

    public void SetDensity(DensityOption value)
    {
        if (value == Density) return;
        Density = value;
        _ = PersistAsync();
        _ = ApplyRootAsync();
        Changed?.Invoke();
    }

    public void SetRowEmphasis(RowEmphasisOption value)
    {
        if (value == RowEmphasis) return;
        RowEmphasis = value;
        _ = PersistAsync();
        _ = ApplyRootAsync();
        Changed?.Invoke();
    }

    public void SetSourceDefault(SourceDefaultOption value)
    {
        if (value == SourceDefault) return;
        SourceDefault = value;
        _ = PersistAsync();
        Changed?.Invoke();
    }

    [JSInvokable]
    public void OnOsColorSchemeChanged(bool isDark)
    {
        if (!FollowOs) return;
        SetDark(isDark);
    }

    public (bool Dark, string Accent, string Density, string RowEmphasis) RootAttributes()
        => (Dark,
            Accent.ToString().ToLowerInvariant(),
            Density.ToString().ToLowerInvariant(),
            RowEmphasis.ToString().ToLowerInvariant());

    public void Dispose()
    {
        if (_dotNetRef != null) _ = UnwatchOsAsync();
        _dotNetRef?.Dispose();
        _dotNetRef = null;
    }

    private string ToJson() => JsonSerializer.Serialize(new
    {
        dark     = Dark,
        followOs = FollowOs,
        accent   = Accent.ToString().ToLowerInvariant(),
        density  = Density.ToString().ToLowerInvariant(),
        rowEmphasis = RowEmphasis.ToString().ToLowerInvariant(),
        source   = SourceDefault.ToString().ToLowerInvariant()
    });

    private async Task PersistAsync()
    {
        try { await _js.InvokeVoidAsync("docview.tweaks.write", ToJson()); }
        catch { /* JS helper already warns */ }
    }

    /// <summary>
    /// The exact <c>(dark, accent, density, rowEmphasis)</c> token tuple fed to
    /// <c>docview.tweaks.applyRoot</c>, which the JS side writes verbatim to the
    /// document element's <c>data-*</c> attributes. This is the single source of
    /// the C# side of the user-visible DOM contract; assert against it to pin the
    /// token mapping without a browser.
    /// </summary>
    private async Task ApplyRootAsync()
    {
        try
        {
            var a = RootAttributes();
            await _js.InvokeVoidAsync(
                "docview.tweaks.applyRoot",
                a.Dark, a.Accent, a.Density, a.RowEmphasis);
        }
        catch { /* best-effort */ }
    }

    private async Task WatchOsAsync()
    {
        try { await _js.InvokeVoidAsync("docview.tweaks.watchOs", _dotNetRef); }
        catch { /* best-effort */ }
    }

    private async Task UnwatchOsAsync()
    {
        try { await _js.InvokeVoidAsync("docview.tweaks.unwatchOs"); }
        catch { /* best-effort */ }
    }

    private async Task SyncOsDarkAsync()
    {
        try
        {
            var isDark = await _js.InvokeAsync<bool>("docview.prefersDark");
            if (isDark != Dark)
            {
                Dark = isDark;
                _ = ApplyRootAsync();
                Changed?.Invoke();
            }
        }
        catch { /* best-effort */ }
    }

    private sealed class Dto
    {
        public bool?   dark        { get; set; }
        public bool?   followOs    { get; set; }
        public string? accent      { get; set; }
        public string? density     { get; set; }
        public string? rowEmphasis { get; set; }
        public string? source      { get; set; }
    }
}
