using System.Text.Json;
using Microsoft.JSInterop;

namespace Delta.DocView.Client.Services;

/// <summary>
/// <see cref="IFavouritesStore"/> backed by the browser's localStorage via the
/// <c>docview.favourites</c> JS helpers (storage key owned by the JS side).
/// <para>
/// Mutations to the in-memory set are synchronous: <see cref="Toggle"/> updates
/// <c>_ids</c>, raises <see cref="Changed"/>, then fires a write back to localStorage
/// as a fire-and-forget JSInterop call. The in-memory set is always authoritative
/// for the current session; the JS write is best-effort and any failure is swallowed
/// (the JS helper already warns).
/// </para>
/// <para>
/// <see cref="InitializeAsync"/> hydrates the set on boot. Any JSON or JS failure
/// during hydration is logged and leaves the set empty — it does NOT rethrow and
/// does NOT raise <see cref="Changed"/>.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Lifecycle contract.</b> <see cref="InitializeAsync"/> MUST complete before any
/// <see cref="Toggle"/> call. App.razor gates MainLayout rendering on
/// <c>Task.WhenAll(LoadAsync, InitializeAsync)</c>. Refactoring the boot sequence
/// requires preserving this invariant. A second call to <see cref="InitializeAsync"/>
/// throws <see cref="InvalidOperationException"/>.
/// </para>
/// <para>
/// <b>Persistence contract (mirrored in <c>docview.js</c>).</b>
/// <list type="bullet">
/// <item><description>Storage key: <c>docview.favs.v1</c></description></item>
/// <item><description>Payload shape: JSON <c>string[]</c> of step ids</description></item>
/// <item><description>Sort order on write: ordinal ascending</description></item>
/// <item><description>Malformed JSON / JS errors: treated as empty set, logged</description></item>
/// <item><description>Migration policy: changing the payload shape requires bumping the
/// key suffix (e.g. <c>.v2</c>) in both this class's JS helpers and <c>docview.js</c>.</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class LocalStorageFavouritesStore : IFavouritesStore
{
    private const string JsRead  = "docview.favourites.read";
    private const string JsWrite = "docview.favourites.write";

    private readonly IJSRuntime _js;
    private readonly HashSet<string> _ids = new();
    private bool _initialized;

    public LocalStorageFavouritesStore(IJSRuntime js)
    {
        _js = js;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) throw new InvalidOperationException("LocalStorageFavouritesStore.InitializeAsync called more than once.");

        try
        {
            var json = await _js.InvokeAsync<string>(JsRead);
            var ids = JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
            _ids.Clear();
            foreach (var id in ids)
            {
                _ids.Add(id);
            }
            _initialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LocalStorageFavouritesStore.InitializeAsync failed: {ex.Message}");
            _ids.Clear();
            // Mark initialized even on failure: a second call should not retry hydration —
            // the in-memory set is now the source of truth for the session.
            _initialized = true;
        }
    }

    public bool Has(string id) => _ids.Contains(id);

    public void Toggle(string id)
    {
        if (!_ids.Add(id)) _ids.Remove(id);
        Changed?.Invoke();
        _ = WriteAsync();
    }

    public int Count => _ids.Count;

    public IReadOnlyCollection<string> All => _ids.ToArray();

    public event Action? Changed;

    private async Task WriteAsync()
    {
        try
        {
            var ordered = _ids.OrderBy(s => s, StringComparer.Ordinal).ToArray();
            var json = JsonSerializer.Serialize(ordered);
            await _js.InvokeVoidAsync(JsWrite, json);
        }
        catch (Exception ex)
        {
            // JS helper already warns; log here so the C# side has a trail too.
            Console.WriteLine($"LocalStorageFavouritesStore.WriteAsync failed: {ex.Message}");
        }
    }
}
