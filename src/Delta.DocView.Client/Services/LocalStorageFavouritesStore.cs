using System.Text.Json;
using Microsoft.JSInterop;

namespace Delta.DocView.Client.Services;

/// <summary>
/// <see cref="IFavouritesStore"/> backed by the browser's localStorage via the
/// <c>docview.favourites</c> JS helpers (storage key owned by the JS side).
/// <para>
/// Mutations to the in-memory set are synchronous: <see cref="FavouritesStoreBase.Toggle"/>
/// updates the set, raises <see cref="FavouritesStoreBase.Changed"/>, then fires a write back
/// to localStorage as a fire-and-forget JSInterop call. The in-memory set is always
/// authoritative for the current session; the JS write is best-effort and any failure is
/// swallowed (the JS helper already warns).
/// </para>
/// <para>
/// <see cref="InitializeAsync"/> hydrates the set on boot. Any JSON or JS failure
/// during hydration is logged and leaves the set empty — it does NOT rethrow and
/// does NOT raise <see cref="FavouritesStoreBase.Changed"/>.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Lifecycle contract.</b> <see cref="InitializeAsync"/> MUST complete before any
/// <see cref="FavouritesStoreBase.Toggle"/> call. App.razor gates MainLayout rendering on
/// <c>Task.WhenAll(LoadAsync, InitializeAsync)</c>. Refactoring the boot sequence
/// requires preserving this invariant. <see cref="InitializeAsync"/> is idempotent:
/// a second call no-ops (it does not re-hydrate from JS or raise
/// <see cref="FavouritesStoreBase.Changed"/>).
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
public sealed class LocalStorageFavouritesStore : FavouritesStoreBase
{
    private const string JsRead  = "docview.favourites.read";
    private const string JsWrite = "docview.favourites.write";

    private readonly IJSRuntime _js;
    private bool _initialized;

    public LocalStorageFavouritesStore(IJSRuntime js)
    {
        _js = js;
    }

    public override async Task InitializeAsync()
    {
        // Idempotent: repeat calls no-op (App.razor awaits this once at boot).
        if (_initialized) return;

        try
        {
            var json = await _js.InvokeAsync<string>(JsRead);
            var ids = JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
            Hydrate(ids);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LocalStorageFavouritesStore.InitializeAsync failed: {ex.Message}");
            Hydrate(Array.Empty<string>());
            // Mark initialized even on failure: a second call should not retry hydration —
            // the in-memory set is now the source of truth for the session.
        }
        _initialized = true;
    }

    protected override async Task PersistAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(Snapshot());
            await _js.InvokeVoidAsync(JsWrite, json);
        }
        catch (Exception ex)
        {
            // JS helper already warns; log here so the C# side has a trail too.
            Console.WriteLine($"LocalStorageFavouritesStore.PersistAsync failed: {ex.Message}");
        }
    }
}
