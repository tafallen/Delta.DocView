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
public sealed class LocalStorageFavouritesStore : IFavouritesStore
{
    private readonly IJSRuntime _js;
    private readonly HashSet<string> _ids = new();

    public LocalStorageFavouritesStore(IJSRuntime js)
    {
        _js = js;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var json = await _js.InvokeAsync<string>("docview.favourites.read");
            var ids = JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
            _ids.Clear();
            foreach (var id in ids)
            {
                _ids.Add(id);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LocalStorageFavouritesStore.InitializeAsync failed: {ex.Message}");
            _ids.Clear();
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
            await _js.InvokeVoidAsync("docview.favourites.write", json);
        }
        catch
        {
            // JS helper already warns; swallow to keep in-memory set consistent.
        }
    }
}
