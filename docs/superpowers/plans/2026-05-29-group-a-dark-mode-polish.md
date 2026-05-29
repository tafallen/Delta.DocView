# Group A — Dark Mode Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Two dark-mode improvements: per-accent `--accent-500` tuning in dark mode (#48), and a "Follow OS" toggle that subscribes to `matchMedia` change events so the app auto-switches when the OS switches (#47).

**Architecture:** #48 is pure CSS — three `[data-dark][data-accent]` selector blocks. #47 spans three layers: JS (`watchOs`/`unwatchOs` in `docview.tweaks`), C# (`TweaksStore` gains `FollowOs`, `SetFollowOs`, `[JSInvokable] OnOsColorSchemeChanged`, `IDisposable`), and Razor (`TweaksPanel` gets a Follow OS checkbox that disables the manual dark toggle).

**Tech Stack:** Blazor WASM, Microsoft.JSInterop, DotNetObjectReference, CSS custom properties, `window.matchMedia`

---

### Task 1: Per-accent dark-mode CSS (#48)

**Files:**
- Modify: `src/Delta.DocView.Client/wwwroot/css/app.css`

The existing `[data-dark="true"]` block sets `--accent-500: #f4823f` for all accents. Blue (`#409dd7`) and violet (`#96519f`) are light-mode values — too dark/muddy on dark backgrounds. We replace the single override with three per-accent blocks.

- [ ] **Step 1: Open `app.css` and find the `[data-dark="true"]` block (around line 65)**

The block currently contains:
```css
[data-dark="true"] {
  ...
  --accent-500: #f4823f;
  --accent-100: oklch(0.34 0.06 55);
  ...
}
```

- [ ] **Step 2: Remove the `--accent-500` and `--accent-100` lines from `[data-dark="true"]`**

After removal the block should NOT contain `--accent-500` or `--accent-100`.

- [ ] **Step 3: Add per-accent dark overrides immediately after the closing `}` of `[data-dark="true"]`, before the `/* ── Accent variants ──` comment**

```css
/* ── Per-accent dark overrides ───────────────────────────────────── */
[data-dark="true"][data-accent="orange"],
[data-dark="true"]:not([data-accent]),
[data-dark="true"][data-accent=""] {
  --accent-500: #f4823f;
  --accent-600: oklch(0.58 0.18 48);
  --accent-100: oklch(0.34 0.06 55);
}
[data-dark="true"][data-accent="blue"] {
  --accent-500: oklch(0.72 0.20 240);
  --accent-600: oklch(0.62 0.18 240);
  --accent-100: oklch(0.30 0.08 240);
}
[data-dark="true"][data-accent="violet"] {
  --accent-500: oklch(0.72 0.18 290);
  --accent-600: oklch(0.62 0.16 290);
  --accent-100: oklch(0.30 0.07 290);
}
```

- [ ] **Step 4: Verify visually**

Restart the dev server. Open the app. Open the Appearance panel, enable dark mode, then switch accent to Blue — the header button, step-type active border, and add-to-scenario button should all appear as a bright readable blue, not the muted `#409dd7`. Switch to Violet and confirm similarly. Switch to Orange and confirm it still looks like before.

- [ ] **Step 5: Commit**

```bash
git add src/Delta.DocView.Client/wwwroot/css/app.css
git commit -m "feat: per-accent dark-mode accent colour overrides (#48)"
```

---

### Task 2: JS `watchOs` / `unwatchOs` (#47 — JS layer)

**Files:**
- Modify: `src/Delta.DocView.Client/wwwroot/js/docview.js`

Add two functions to the `docview.tweaks` object. `watchOs(dotNetRef)` attaches a `matchMedia` listener; `unwatchOs()` removes it. Both are idempotent.

- [ ] **Step 1: Open `docview.js` and find the `tweaks:` object (around line 190)**

The object currently ends with `applyRoot: function(...) { ... }` then closes with `}` (and then the outer `}`).

- [ ] **Step 2: Add `_mql`, `_mqlListener`, `watchOs`, and `unwatchOs` to the `tweaks` object**

Add a comma after the closing `}` of `applyRoot`, then add:

```javascript
        _mql: null,
        _mqlListener: null,
        watchOs: function (dotNetRef) {
            try {
                if (this._mqlListener) return; // idempotent — already watching
                this._mql = window.matchMedia('(prefers-color-scheme: dark)');
                var self = this;
                this._mqlListener = function (e) {
                    dotNetRef.invokeMethodAsync('OnOsColorSchemeChanged', e.matches);
                };
                this._mql.addEventListener('change', this._mqlListener);
            } catch (e) { console.warn('docview.tweaks.watchOs failed', e); }
        },
        unwatchOs: function () {
            try {
                if (this._mql && this._mqlListener) {
                    this._mql.removeEventListener('change', this._mqlListener);
                }
                this._mql = null;
                this._mqlListener = null;
            } catch (e) { console.warn('docview.tweaks.unwatchOs failed', e); }
        }
```

The final `tweaks` object should look like (abbreviated):
```javascript
tweaks: {
    read: function () { ... },
    write: function (json) { ... },
    applyRoot: function (dark, accent, density, rowEmphasis) { ... },
    _mql: null,
    _mqlListener: null,
    watchOs: function (dotNetRef) { ... },
    unwatchOs: function () { ... }
}
```

- [ ] **Step 3: Verify the file parses without JS errors**

Open the app in a browser — if there are JS syntax errors they'll appear in the browser console on load.

- [ ] **Step 4: Commit**

```bash
git add src/Delta.DocView.Client/wwwroot/js/docview.js
git commit -m "feat: add watchOs/unwatchOs to docview.tweaks for OS dark-mode listening (#47)"
```

---

### Task 3: TweaksStore — FollowOs, IDisposable, tests (#47 — C# layer)

**Files:**
- Modify: `src/Delta.DocView.Client/Services/TweaksStore.cs`
- Modify: `tests/Delta.DocView.Tests/Services/TweaksStoreTests.cs`

`TweaksStore` gains `FollowOs`, `SetFollowOs`, `[JSInvokable] OnOsColorSchemeChanged`, `IDisposable`, updated `InitializeAsync`, updated `ToJson`/`Dto`.

- [ ] **Step 1: Write failing tests first**

In `TweaksStoreTests.cs`, add these tests and extend `RecordingJsRuntime`:

```csharp
// Add to RecordingJsRuntime:
public int WatchOsCalls { get; private set; }
public int UnwatchOsCalls { get; private set; }

// Add to InvokeAsync switch:
case "docview.tweaks.watchOs":
    WatchOsCalls++;
    break;
case "docview.tweaks.unwatchOs":
    UnwatchOsCalls++;
    break;
```

Add these test methods to `TweaksStoreTests`:

```csharp
[Fact]
public void Defaults_Before_Init_FollowOs_False()
{
    var store = new TweaksStore(Substitute.For<IJSRuntime>());
    Assert.False(store.FollowOs);
}

[Fact]
public async Task SetFollowOs_True_CallsWatchOs_AndRaisesChanged()
{
    var js = new RecordingJsRuntime { ReadResponse = "" };
    var store = new TweaksStore(js);
    await store.InitializeAsync();
    var changed = 0;
    store.Changed += () => changed++;

    store.SetFollowOs(true);
    await Task.Yield();
    await Task.Yield();

    Assert.True(store.FollowOs);
    Assert.Equal(1, changed);
    Assert.Equal(1, js.WatchOsCalls);
}

[Fact]
public async Task SetFollowOs_False_CallsUnwatchOs_AndRaisesChanged()
{
    var js = new RecordingJsRuntime { ReadResponse = "" };
    var store = new TweaksStore(js);
    await store.InitializeAsync();
    store.SetFollowOs(true);
    await Task.Yield();
    await Task.Yield();
    var changed = 0;
    store.Changed += () => changed++;

    store.SetFollowOs(false);
    await Task.Yield();
    await Task.Yield();

    Assert.False(store.FollowOs);
    Assert.Equal(1, changed);
    Assert.Equal(1, js.UnwatchOsCalls);
}

[Fact]
public async Task SetFollowOs_SameValue_NoChange_NoEvent()
{
    var js = new RecordingJsRuntime { ReadResponse = "" };
    var store = new TweaksStore(js);
    await store.InitializeAsync();
    var changed = 0;
    store.Changed += () => changed++;

    store.SetFollowOs(false); // already false

    Assert.Equal(0, changed);
    Assert.Equal(0, js.WatchOsCalls);
}

[Fact]
public async Task OnOsColorSchemeChanged_WhenFollowOs_SetsDark()
{
    var js = new RecordingJsRuntime { ReadResponse = "" };
    var store = new TweaksStore(js);
    await store.InitializeAsync();
    store.SetFollowOs(true);

    store.OnOsColorSchemeChanged(true);

    Assert.True(store.Dark);
}

[Fact]
public async Task OnOsColorSchemeChanged_WhenNotFollowOs_IsNoOp()
{
    var js = new RecordingJsRuntime { ReadResponse = "" };
    var store = new TweaksStore(js);
    await store.InitializeAsync();
    // FollowOs defaults to false

    store.OnOsColorSchemeChanged(true);

    Assert.False(store.Dark); // unchanged
}

[Fact]
public async Task Dispose_CallsUnwatchOs()
{
    var js = new RecordingJsRuntime { ReadResponse = "" };
    var store = new TweaksStore(js);
    await store.InitializeAsync();
    store.SetFollowOs(true);
    await Task.Yield();
    await Task.Yield();

    store.Dispose();
    await Task.Yield();
    await Task.Yield();

    Assert.True(js.UnwatchOsCalls >= 1);
}

[Fact]
public async Task InitializeAsync_WithFollowOs_RestoresListenerAndUsesOsDark()
{
    var js = new RecordingJsRuntime
    {
        ReadResponse = "{\"dark\":false,\"followOs\":true,\"accent\":\"orange\",\"density\":\"comfortable\",\"rowEmphasis\":\"pattern\",\"source\":\"collapsed\"}",
        PrefersDarkResponse = true
    };
    var store = new TweaksStore(js);

    await store.InitializeAsync();
    await Task.Yield();
    await Task.Yield();

    Assert.True(store.FollowOs);
    Assert.True(store.Dark);   // from OS preference, not stored dark:false
    Assert.Equal(1, js.WatchOsCalls);
}

[Fact]
public async Task SetFollowOs_Persists_FollowOs_InJson()
{
    var js = new RecordingJsRuntime { ReadResponse = "" };
    var store = new TweaksStore(js);
    await store.InitializeAsync();

    store.SetFollowOs(true);
    await Task.Yield();
    await Task.Yield();

    Assert.NotEmpty(js.WriteCalls);
    Assert.Contains("\"followOs\":true", js.WriteCalls[^1]);
}
```

- [ ] **Step 2: Run tests to confirm they all fail**

```
dotnet test tests/Delta.DocView.Tests/Delta.DocView.Tests.csproj --filter "TweaksStore" -v n
```

Expected: multiple failures about missing members.

- [ ] **Step 3: Update `TweaksStore.cs`**

Replace the entire file with:

```csharp
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

    public bool Dark      { get; private set; }
    public bool FollowOs  { get; private set; }
    public AccentOption       Accent        { get; private set; } = AccentOption.Orange;
    public DensityOption      Density       { get; private set; } = DensityOption.Comfortable;
    public RowEmphasisOption  RowEmphasis   { get; private set; } = RowEmphasisOption.Pattern;
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
        _ = UnwatchOsAsync();
        _dotNetRef?.Dispose();
        _dotNetRef = null;
    }

    private string ToJson() => JsonSerializer.Serialize(new
    {
        dark = Dark,
        followOs = FollowOs,
        accent = Accent.ToString().ToLowerInvariant(),
        density = Density.ToString().ToLowerInvariant(),
        rowEmphasis = RowEmphasis.ToString().ToLowerInvariant(),
        source = SourceDefault.ToString().ToLowerInvariant()
    });

    private async Task PersistAsync()
    {
        try { await _js.InvokeVoidAsync("docview.tweaks.write", ToJson()); }
        catch { /* JS helper already warns */ }
    }

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

    private sealed class Dto
    {
        public bool? dark      { get; set; }
        public bool? followOs  { get; set; }
        public string? accent     { get; set; }
        public string? density    { get; set; }
        public string? rowEmphasis { get; set; }
        public string? source     { get; set; }
    }
}
```

- [ ] **Step 4: Run tests**

```
dotnet test tests/Delta.DocView.Tests/Delta.DocView.Tests.csproj --filter "TweaksStore" -v n
```

Expected: all TweaksStore tests pass.

- [ ] **Step 5: Run full test suite**

```
dotnet test tests/Delta.DocView.Tests/Delta.DocView.Tests.csproj -v n
```

Expected: all tests pass, 0 failures.

- [ ] **Step 6: Commit**

```bash
git add src/Delta.DocView.Client/Services/TweaksStore.cs
git add tests/Delta.DocView.Tests/Services/TweaksStoreTests.cs
git commit -m "feat: TweaksStore FollowOs + IDisposable for OS dark-mode listening (#47)"
```

---

### Task 4: TweaksPanel — Follow OS toggle UI (#47 — Razor)

**Files:**
- Modify: `src/Delta.DocView.Client/Components/TweaksPanel.razor`

Add a "Follow OS" checkbox below the Dark mode toggle. When checked, the manual Dark mode toggle is disabled.

- [ ] **Step 1: Write the failing bUnit test**

In `tests/Delta.DocView.Tests/Components/TweaksPanelTests.cs` (or whichever file covers TweaksPanel), add:

```csharp
[Fact]
public void FollowOs_Checkbox_Renders_And_Calls_SetFollowOs()
{
    // Arrange
    var tweaks = Substitute.For<TweaksStore>(Substitute.For<IJSRuntime>());
    using var ctx = new TestContext();
    ctx.Services.AddSingleton(tweaks);
    ctx.Services.AddSingleton(Substitute.For<TweaksPanelState>());
    ctx.Services.AddSingleton(Substitute.For<IJSRuntime>());

    var cut = ctx.RenderComponent<TweaksPanel>();

    // Act
    var cb = cut.Find("[data-testid='tweaks-follow-os']");
    cb.Change(true);

    // Assert
    tweaks.Received(1).SetFollowOs(true);
}

[Fact]
public void DarkToggle_Disabled_When_FollowOs_True()
{
    var tweaks = Substitute.For<TweaksStore>(Substitute.For<IJSRuntime>());
    tweaks.FollowOs.Returns(true);
    using var ctx = new TestContext();
    ctx.Services.AddSingleton(tweaks);
    ctx.Services.AddSingleton(Substitute.For<TweaksPanelState>());
    ctx.Services.AddSingleton(Substitute.For<IJSRuntime>());

    var cut = ctx.RenderComponent<TweaksPanel>();

    var darkCb = cut.Find("[data-testid='tweaks-dark']");
    Assert.True(darkCb.HasAttribute("disabled"));
}
```

- [ ] **Step 2: Run tests to confirm they fail**

```
dotnet test tests/Delta.DocView.Tests/Delta.DocView.Tests.csproj --filter "TweaksPanel" -v n
```

Expected: the two new tests fail (element not found / no `disabled`).

- [ ] **Step 3: Update `TweaksPanel.razor` dark mode row and add Follow OS row**

Replace the existing dark mode label block:

```razor
<label class="tweak-row">
    <span>Dark mode</span>
    <input type="checkbox" data-testid="tweaks-dark"
           checked="@Tweaks.Dark"
           @onchange="e => Tweaks.SetDark((bool)e.Value!)" />
</label>
```

With:

```razor
<label class="tweak-row">
    <span>Dark mode</span>
    <input type="checkbox" data-testid="tweaks-dark"
           checked="@Tweaks.Dark"
           disabled="@Tweaks.FollowOs"
           @onchange="e => Tweaks.SetDark((bool)e.Value!)" />
</label>
<label class="tweak-row">
    <span>Follow OS</span>
    <input type="checkbox" data-testid="tweaks-follow-os"
           checked="@Tweaks.FollowOs"
           @onchange="e => Tweaks.SetFollowOs((bool)e.Value!)" />
</label>
```

- [ ] **Step 4: Run tests**

```
dotnet test tests/Delta.DocView.Tests/Delta.DocView.Tests.csproj --filter "TweaksPanel" -v n
```

Expected: all TweaksPanel tests pass.

- [ ] **Step 5: Run full test suite**

```
dotnet test tests/Delta.DocView.Tests/Delta.DocView.Tests.csproj -v n
```

Expected: all tests pass, 0 failures.

- [ ] **Step 6: Verify manually**

Restart the dev server. Open the Appearance panel. Confirm:
- "Follow OS" checkbox appears below "Dark mode"
- Ticking "Follow OS" greys out the "Dark mode" checkbox
- Switching OS dark/light preference (browser devtools → Rendering → Emulate CSS prefers-color-scheme) updates the app live while "Follow OS" is checked
- Unticking "Follow OS" re-enables the manual toggle
- Refreshing the page restores the "Follow OS" state from localStorage

- [ ] **Step 7: Commit**

```bash
git add src/Delta.DocView.Client/Components/TweaksPanel.razor
git commit -m "feat: Follow OS toggle in TweaksPanel disables manual dark control (#47)"
```

---

## Closing

Close issues:

```bash
gh issue close 47 --comment "Implemented: Follow OS toggle in TweaksPanel subscribes to matchMedia; manual dark toggle disabled while following."
gh issue close 48 --comment "Implemented: per-accent dark-mode --accent-500/600/100 overrides for orange, blue, violet."
```
