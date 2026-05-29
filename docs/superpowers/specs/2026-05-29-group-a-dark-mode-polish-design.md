# Group A — Dark Mode Polish Design

## Scope

Two independent but thematically related improvements to dark-mode handling, both deferred from US-11.

- **#47** Live OS dark-mode detection while app is open
- **#48** Per-accent dark-mode colour overrides

---

## #47 — Live OS Dark-Mode Detection

### Problem

`TweaksStore.InitializeAsync` reads `prefers-color-scheme` once at boot via `docview.prefersDark()`. If the OS switches dark/light while the app is open and the user has never manually set a preference, the app stays stale.

### Design Decision

Add a **"Follow OS"** toggle. When enabled, the app subscribes to `matchMedia` change events and auto-switches. Manual dark/light toggle is disabled while "Follow OS" is on. Users who prefer manual control are unaffected.

### Architecture

**`TweaksStore` changes (`TweaksStore.cs`)**

- Add `bool FollowOs { get; private set; }` (default `false`)
- Add `SetFollowOs(bool value)` — persists, applies, fires `Changed`
- Add `[JSInvokable] public void OnOsColorSchemeChanged(bool isDark)` — calls `SetDark(isDark)` internally (skips if `!FollowOs`)
- Create a `DotNetObjectReference<TweaksStore>` on first `SetFollowOs(true)` call; reuse across subsequent toggles
- Implement `IDisposable`: call `docview.tweaks.unwatchOs()` and dispose the `DotNetObjectReference` on teardown
- Persist `followOs` in the existing `docview.tweaks.v1` JSON blob (additive field; no key bump required)
- On `InitializeAsync`: if persisted `followOs` is true, call `SetFollowOs(true)` to re-attach the listener

**`docview.js` changes**

- Add `docview.tweaks.watchOs(dotNetRef)` — creates a `matchMedia('(prefers-color-scheme: dark)')` listener; on change calls `dotNetRef.invokeMethodAsync('OnOsColorSchemeChanged', e.matches)`; stores listener reference for cleanup
- Add `docview.tweaks.unwatchOs()` — removes the stored listener via `removeEventListener`; no-op if not watching

**Tweaks UI panel**

- Add a "Follow OS" toggle (checkbox or switch) in the appearance section
- When `FollowOs` is true, the manual dark/light toggle is rendered `disabled`

### Persistence

The `docview.tweaks.v1` JSON gains a `followOs` boolean field. Absence of the field (old stored values) defaults to `false` — backward compatible.

### Error handling

JS interop errors in `watchOs`/`unwatchOs` are swallowed (best-effort, same pattern as existing `applyRoot`).

---

## #48 — Per-Accent Dark-Mode Colour Overrides

### Problem

Dark mode applies a single `--accent-500` override regardless of accent. Orange looks fine; blue and violet use the same swap mechanism but their dark-mode tints are not optimised for dark backgrounds, resulting in low contrast or muddy colours.

### Design Decision

Tune only `--accent-500` per accent in dark mode (option A). No changes to chip backgrounds or surface tints.

### Architecture

**`app.css` changes only**

Replace the current single dark-mode `--accent-500` override with three per-accent blocks:

```css
[data-dark="true"][data-accent="orange"] { --accent-500: <warm bright orange>; }
[data-dark="true"][data-accent="blue"]   { --accent-500: oklch(0.72 0.20 240); }
[data-dark="true"][data-accent="violet"] { --accent-500: oklch(0.72 0.18 290); }
```

Orange retains its existing dark value. Blue and violet get lighter, higher-chroma values suited to dark backgrounds.

No C# changes. No JS changes.

---

## Files Touched

| File | Change |
|---|---|
| `src/Delta.DocView.Client/Services/TweaksStore.cs` | Add `FollowOs`, `SetFollowOs`, `OnOsColorSchemeChanged`, `IDisposable` update |
| `src/Delta.DocView.Client/wwwroot/js/docview.js` | Add `watchOs`, `unwatchOs` to `docview.tweaks` |
| `src/Delta.DocView.Client/Components/TweaksPanel.razor` | Add "Follow OS" toggle, disable manual toggle when following |
| `src/Delta.DocView.Client/wwwroot/css/app.css` | Per-accent dark `--accent-500` overrides |

## Testing

- `TweaksStore`: unit tests for `SetFollowOs`, `OnOsColorSchemeChanged` (mock JS runtime)
- `TweaksStore`: verify `RootAttributes()` output unchanged (followOs is not a DOM attribute)
- `TweaksStore`: verify `Dispose` calls `unwatchOs` (mock JS verify)
- CSS: manual visual check — switch accent to blue/violet in dark mode, confirm accent colour is bright and readable
- #47 integration: toggle "Follow OS" on, simulate OS switch via browser devtools, confirm app switches
