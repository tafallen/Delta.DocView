# Delta.DocView — US-05 Favourites Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans`. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Replace the in-memory v1 `IFavouritesStore` with a localStorage-backed implementation so favourites survive a page refresh. The interface and every call site (`StepRow.OnStarClick`, `FavouritesToggle`, `FilterEngine`, future detail-panel star in US-06) stay identical — the swap is purely in the DI registration.

**Architecture:**

- A new `LocalStorageFavouritesStore` implements `IFavouritesStore` (the interface that already includes `InitializeAsync()` — added in commit `4432770` for exactly this story).
- `InitializeAsync()` reads the JSON array under storage key `docview.favs.v1` via a JS helper `docview.favourites.read()`. Empty / missing / malformed → empty set + warning logged.
- Sync mutations (`Toggle`) update the in-memory set, raise `Changed`, then fire-and-forget the persistence write via `docview.favourites.write(ids)`. UI updates instantly; localStorage races (acceptable — write is cheap and idempotent).
- `Has`, `Count`, `All` read from the same in-memory `HashSet<string>`.
- The existing `InMemoryFavouritesStore` is kept (test fixture for component tests) but is no longer the production registration.
- `App.razor` already awaits `Favourites.InitializeAsync()` in parallel with `LibraryClient.LoadAsync()` via `Task.WhenAll`. No render of `MainLayout` until both complete, so no `Toggle` call can race the hydration.

**Tech stack additions:** none.

---

## File Map

```
src/Delta.DocView.Client/
  Services/
    LocalStorageFavouritesStore.cs    ← new (production v1 impl)
    InMemoryFavouritesStore.cs        ← unchanged (kept for test DI)
    IFavouritesStore.cs               ← unchanged (already has InitializeAsync)
  Program.cs                          ← UPDATED: register LocalStorageFavouritesStore
  wwwroot/js/docview.js               ← UPDATED: add favourites.read / favourites.write

tests/Delta.DocView.Tests/
  Services/
    LocalStorageFavouritesStoreTests.cs   ← new (mocks IJSRuntime)
```

**Note**: existing component / integration tests register `IFavouritesStore -> InMemoryFavouritesStore` directly in their `TestContext`. None of those tests touch JSInterop for favourites; the swap to LocalStorage in production DI doesn't affect them.

---

## Design notes

- **Storage key**: `docview.favs.v1`. Versioned suffix anticipates future schema migrations; there's no prior version to migrate from.
- **Storage shape**: a JSON array of step ids, e.g. `["auth-001a", "billing-0042"]`. Not an object; the array is the simplest stable shape and matches what the spec says ("a JSON array of step IDs").
- **Read path**: `JsonSerializer.Deserialize<string[]>` on the raw string. Null / empty / `JsonException` / `JSException` → empty set. Log the exception via `Console.WriteLine` (Blazor WASM has no `ILogger<>` wired in by default; if a logger is available, prefer that — check `Program.cs`).
- **Write path**: serialise current set ordered ordinal-ascending (stable output) and call `docview.favourites.write(jsonString)`. JS side just `localStorage.setItem('docview.favs.v1', jsonString)`. Fire-and-forget — `_ = WriteAsync()` with try/catch swallowing.
- **Quota exceeded**: caught in JS, propagated as a JSException to .NET — log and continue. In-memory set stays consistent; localStorage just won't persist that mutation. Acceptable for v1.
- **Cross-tab sync**: explicitly **out of scope**. We could subscribe to the `window.storage` event in JS and call a .NET callback, but the spec doesn't require it and the surface area is non-trivial. Document as deferred.
- **Toggle pre-init**: not possible by construction — `App.razor`'s `Task.WhenAll(LoadAsync, InitializeAsync)` gates `MainLayout` rendering. Any star button only mounts after both complete. No defensive flag needed in the store.
- **Changed event semantics**: `InitializeAsync` does NOT raise `Changed` (no subscribers exist yet during init). `Toggle` raises `Changed` synchronously BEFORE the localStorage write completes — UI gets immediate feedback, localStorage races.
- **`InMemoryFavouritesStore` lifecycle**: keep as-is, mark with a brief doc comment `<remarks>Used by tests; production registration is LocalStorageFavouritesStore as of US-05.</remarks>`.

---

## Tasks

### Task 1 — JS helpers in `docview.js`

- [ ] Append to `src/Delta.DocView.Client/wwwroot/js/docview.js` (next to `setDark` and `setDomainPalette`):
    ```js
    favourites: {
        read: function () {
            try {
                const raw = window.localStorage.getItem('docview.favs.v1');
                return raw === null ? '[]' : raw;
            } catch (e) {
                console.warn('docview.favourites.read failed', e);
                return '[]';
            }
        },
        write: function (json) {
            try {
                window.localStorage.setItem('docview.favs.v1', json);
            } catch (e) {
                console.warn('docview.favourites.write failed', e);
            }
        }
    }
    ```
- [ ] No tests for this file directly; behaviour is verified via the .NET-side store tests which mock `IJSRuntime`.

### Task 2 — `LocalStorageFavouritesStore`

- [ ] Create `src/Delta.DocView.Client/Services/LocalStorageFavouritesStore.cs`. Sealed class implementing `IFavouritesStore`.
- [ ] Constructor takes `IJSRuntime js`. Stores it.
- [ ] Fields: `private readonly HashSet<string> _ids = new();`
- [ ] `Count` → `_ids.Count`. `All` → `_ids.ToArray()` (snapshot — mirror the InMemory impl). `Has(string id)` → `_ids.Contains(id)`.
- [ ] `Changed` event.
- [ ] `InitializeAsync()`:
    ```csharp
    try
    {
        var json = await _js.InvokeAsync<string>("docview.favourites.read");
        var ids = JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        _ids.Clear();
        foreach (var id in ids) _ids.Add(id);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"LocalStorageFavouritesStore.InitializeAsync failed: {ex.Message}");
    }
    ```
    Does NOT raise `Changed`.
- [ ] `Toggle(string id)`:
    ```csharp
    if (!_ids.Add(id)) _ids.Remove(id);
    Changed?.Invoke();
    _ = WriteAsync();
    ```
    where `WriteAsync` serialises `_ids.OrderBy(s => s, StringComparer.Ordinal)` and calls `docview.favourites.write` in a try/catch that swallows.
- [ ] XML doc the class — call out the fire-and-forget write semantics and the storage key.

- [ ] Tests in `tests/Delta.DocView.Tests/Services/LocalStorageFavouritesStoreTests.cs`:
    1. `InitializeAsync_NoStoredValue_LeavesStoreEmpty`: `IJSRuntime` returns `"[]"` from `docview.favourites.read`. `Count == 0` after init.
    2. `InitializeAsync_RestoresStoredIds`: `IJSRuntime` returns `"[\"a\",\"b\"]"`. After init, `Has("a")` and `Has("b")` are both true; `Count == 2`.
    3. `InitializeAsync_MalformedJson_LeavesStoreEmpty_NoThrow`: returns `"{not valid}"`. No exception escapes; `Count == 0`.
    4. `InitializeAsync_NullReturn_LeavesStoreEmpty`: `IJSRuntime` returns `null` (deserialised). `Count == 0`.
    5. `Toggle_AddsThenRemoves_RaisesChangedEachTime`: subscribe a counter; `Toggle("x")` then `Toggle("x")`; counter is 2; `Has("x")` is false at the end.
    6. `Toggle_PersistsViaJs_WritesOrdinalSortedJson`: subscribe to `IJSRuntime.InvokeVoidAsync("docview.favourites.write", ...)` via NSubstitute and capture the argument. After `Toggle("b"); Toggle("a");` the most recent captured argument is the JSON array `["a","b"]` (ordinal ascending). NSubstitute's `Received().InvokeVoidAsync(...)` with argument matchers covers this; if matcher ergonomics get awkward, use a custom `IJSRuntime` stub that records calls.

### Task 3 — Production DI swap + smoke

- [ ] Update `src/Delta.DocView.Client/Program.cs`: change `builder.Services.AddScoped<IFavouritesStore, InMemoryFavouritesStore>();` to `builder.Services.AddScoped<IFavouritesStore, LocalStorageFavouritesStore>();`.
- [ ] Add a remarks XML comment above `InMemoryFavouritesStore` class declaration: `<remarks>Used by tests for fast, deterministic favourites without JSInterop. Production registration as of US-05 is LocalStorageFavouritesStore.</remarks>`.
- [ ] Manual smoke (DOCUMENTED in the commit message — no automation): start the app, star a step, refresh the page, verify the star remains active and the count badge in the rail still reads 1.
- [ ] No existing tests should break — every test that uses `IFavouritesStore` explicitly registers `InMemoryFavouritesStore` in its `TestContext`. Run the full suite to confirm.
- [ ] Tick the US-05 AC boxes in `USER-STORIES.md` from `[ ]` to `[x]` (except the `F` keyboard shortcut — that's US-07).

### Tasks complete — commit message for Task 3

```
feat: US-05 wire LocalStorageFavouritesStore in production DI (closes #5)

Swap the production IFavouritesStore from the in-memory v1 impl to
LocalStorageFavouritesStore so a starred step survives a page refresh.
The interface contract — InitializeAsync hydration + sync Toggle plus
fire-and-forget JSInterop write — was locked in at the end of US-03
(TD-F, commit 4432770), so no call site changes.

InMemoryFavouritesStore stays in the codebase for test contexts.

Closes #5

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Out of scope (deferred)

- **`F` keyboard shortcut** (in US-05's AC but listed under US-07's implementation tasks): wait for the keyboard handler in US-07. The Toggle call site exists already — only the binding is missing.
- **Detail-panel star button**: that lives in US-06 (detail panel). The favourite-toggle behaviour it calls is already complete.
- **Cross-tab sync**: subscribing to `window.storage` so two open tabs stay in sync. Spec doesn't require it; can land later as a small follow-up.
- **Schema migration from prior versions**: there are no prior versions of the key. Future migrations swap the key suffix and copy on first read.

---

## Risk + open questions

| # | Question | Owner | Decision |
|---|----------|-------|----------|
| 1 | Should `Changed` fire after `InitializeAsync` so a pre-mounted subscriber sees the hydrated set? | Engineering | No — `App.razor` gates render on init completion, so by the time any subscriber attaches, the set is already correct. Firing would be redundant. |
| 2 | Quota / private-mode failures: warn the user, or silently degrade? | Product | Silently degrade for v1. Log to console. A toast UI exists nowhere yet; not worth introducing for this edge. |
| 3 | Should `LocalStorageFavouritesStore` order ids stably before writing? | Engineering | Yes — ordinal ascending. Makes diffs in browser devtools / multi-tab comparisons sane. |
