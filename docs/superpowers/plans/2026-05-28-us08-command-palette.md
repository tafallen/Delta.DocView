# Delta.DocView — US-08 Command Palette Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans`. Steps use checkbox (`- [ ]`) syntax.

**Goal:** A modal command palette opened by `⌘K` / `Ctrl+K` / `/` / the header's "Quick find" button. Default state shows the top 50 steps by usage; typing fuzzy-matches across pattern, type, domain, tags, and param names. Arrow Up/Down navigates, Enter selects, Escape closes. Selecting a result writes to `SelectionState` (which the detail panel already subscribes to).

**Architecture:**

- A new `Scoped` `PaletteState` owns `IsOpen`, `Query`, `Results`, `SelectedIndex`, and a `Changed` event. Subscribes to `IKeyboardActions.OpenPaletteRequested` and `CloseOverlayRequested` in its constructor (the stubs US-07 wired). Disposes the subscriptions.
- A new `Palette.razor` component mounts once in `MainLayout`, renders a backdrop + modal when `PaletteState.IsOpen`, and handles its own `@onkeydown` for Arrow Up/Down/Enter/Escape (the global keyboard handler skips when an input is focused).
- `FuzzySearch.Score(needle, hay)` is a small static scorer: subsequence match with bonuses for word starts and consecutive matches, penalty for null match. Score `0` = no match. Haystack per step concatenates pattern + type + domain + tags + param names.
- Platform detection: a new `IPlatform` service exposes `IsMac` (read once via JS at boot). The header's "Quick find" button label and the palette footer show `⌘K` on macOS, `Ctrl+K` elsewhere. The global keyboard handler in US-07 already accepts both `ctrl+K` and `meta+K`, so no behavioural change there — only the visual shortcut hint differs.
- Selecting a result: `PaletteState.Select(step)` calls `Selection.Select(step)` and then `Close()` itself. The detail panel already subscribes to `SelectionState.Changed`.

**Tech stack additions:** none.

---

## File Map

```
src/Delta.DocView.Client/
  Services/
    IPlatform.cs                 ← interface
    PlatformService.cs           ← impl with InitializeAsync (reads navigator.platform once via JS)
    FuzzySearch.cs               ← static scorer
    PaletteState.cs              ← Scoped state + bus subscriptions
  Components/
    Palette.razor                ← modal overlay
  Layout/
    Header.razor                 ← UPDATED — Quick find button + platform-aware shortcut label
    MainLayout.razor             ← UPDATED — mount <Palette />
  App.razor                      ← UPDATED — await Platform.InitializeAsync in the boot Task.WhenAll
  Program.cs                     ← UPDATED — register IPlatform, PaletteState
  wwwroot/js/docview.js          ← UPDATED — platform.isMac, focus.element/restorePrevious, scrollIntoViewIfNeeded

tests/Delta.DocView.Tests/
  Services/
    PlatformServiceTests.cs
    FuzzySearchTests.cs
    PaletteStateTests.cs
  Components/
    PaletteTests.cs
    HeaderTests.cs               ← EXTENDED — shortcut label reflects platform
  Integration/
    FilterStackTests.cs          ← EXTENDED — palette open / fuzzy match / Enter select / Escape close
```

---

## Design notes

- **`IPlatform`**:
  ```csharp
  public interface IPlatform
  {
      bool IsMac { get; }
      Task InitializeAsync();
      string ShortcutLabel(string letter); // "⌘K" on Mac, "Ctrl+K" elsewhere
  }
  ```
  `ShortcutLabel("K")` returns `"⌘K"` or `"Ctrl+K"`. One method, called from every consumer that displays a chord — no scattering of `IsMac ? "⌘" : "Ctrl+"` ternaries.

- **`PlatformService`**: ctor takes `IJSRuntime`. `InitializeAsync()` calls `docview.platform.isMac()` which returns `bool` from `/Mac/i.test(navigator.platform || navigator.userAgent)`. Default `IsMac = false` until init completes. Init errors → log + assume false. Idempotent.

- **`FuzzySearch.Score(needle, hay)`**: ordinal-case-insensitive. Algorithm:
  - Empty `needle` → return 0 (caller filters empty queries upstream).
  - Walk `hay` char by char, advancing through `needle`. Each match adds a base score of 10.
  - +15 bonus if matched at index 0 of `hay`.
  - +8 bonus if preceded by `' '`, `'.'`, `'_'`, `'-'`, or `'/'` (word boundary).
  - +5 bonus if consecutive with previous match.
  - If `needle` not fully consumed by the end of `hay` → return 0.
  - Otherwise return accumulated score.

- **Haystack assembly**: `$"{step.Pattern} {step.Type} {step.Domain} {string.Join(" ", step.Tags)} {string.Join(" ", step.Params.Select(p => p.Name))}"`. Computed lazily inside the scorer — at ~2k steps × score loop, no caching needed for v1.

- **`PaletteState` surface**:
  ```csharp
  public sealed class PaletteState : IDisposable
  {
      public bool IsOpen { get; private set; }
      public string Query { get; private set; } = "";
      public IReadOnlyList<Step> Results { get; private set; } = Array.Empty<Step>();
      public int SelectedIndex { get; private set; }
      public event Action? Changed;

      public void Open();          // raises Changed; Recompute() seeds default results (top 50 by Used)
      public void Close();         // clears query + selection; raises Changed
      public void SetQuery(string q);
      public void MoveSelectionDown();
      public void MoveSelectionUp();
      public Step? CurrentResult { get; }
      public void SelectCurrent(); // writes to Selection.Select(CurrentResult) then closes
  }
  ```
  Constructor wires `actions.OpenPaletteRequested += Open;` and `actions.CloseOverlayRequested += Close;`. Disposes both. (No need to subscribe to `ToggleComposerRequested` or `OpenShortcutsRequested` — those belong to US-09/10.)

- **Recompute**:
  - Empty query → `Results = Store.Steps.OrderByDescending(s => s.Used).Take(50).ToList(); SelectedIndex = 0;`
  - Non-empty → `Results = Store.Steps.Select(s => (s, FuzzySearch.Score(Query, BuildHaystack(s)))).Where(t => t.Item2 > 0).OrderByDescending(t => t.Item2).Select(t => t.s).Take(50).ToList();`
  - Reset `SelectedIndex` to 0 on every Recompute.

- **Palette component lifecycle**:
  - Subscribes to `PaletteState.Changed`; disposes.
  - When `IsOpen` flips true, on the next render call `docview.focus.element(_inputId)` via JSInterop. `_inputId = $"palette-input-{_uid}"`.
  - On close, call `docview.focus.restorePrevious()`. The JS helper stores `document.activeElement` at the moment `focus.element` was called, and restores it on `restorePrevious`.
  - `@onkeydown` on the input:
    - `ArrowDown` → `State.MoveSelectionDown()`; prevent default.
    - `ArrowUp` → `State.MoveSelectionUp()`; prevent default.
    - `Enter` → `State.SelectCurrent()`; prevent default.
    - `Escape` → `State.Close()`; prevent default. (Backup; the global handler also raises `CloseOverlayRequested` but won't fire while the input has focus — so the local handler is the actual route.)
  - When `SelectedIndex` changes, scroll the active row into view: `docview.scrollIntoViewIfNeeded(elementId)` with `data-result-index="@i"` on each row, target the active one.

- **Markup outline**:
  ```razor
  @if (State.IsOpen)
  {
      <div class="palette-backdrop" data-testid="palette-backdrop" @onclick="State.Close">
          <div class="palette" data-testid="palette" @onclick:stopPropagation @onkeydown="OnKey">
              <input id="@_inputId" class="palette-input" type="text"
                     placeholder="Search steps…"
                     value="@State.Query"
                     @oninput="e => State.SetQuery(e.Value?.ToString() ?? "")"
                     @ref="_inputRef" />
              <div class="palette-meta">@FormatMeta()</div>
              <div class="palette-results" data-testid="palette-results">
                  @for (var i = 0; i < State.Results.Count; i++)
                  {
                      var idx = i;
                      var step = State.Results[i];
                      <button class="palette-result @(idx == State.SelectedIndex ? "is-active" : "")"
                              data-testid="palette-result"
                              data-step-id="@step.Id"
                              data-result-index="@i"
                              @onmouseover="() => State.SetSelectedIndex(idx)"
                              @onclick="() => State.SelectCurrent()">
                          <span class="type-chip chip-@step.Type.ToLowerInvariant()">@step.Type</span>
                          <span class="palette-pattern">
                              <PatternRenderer Pattern="@step.Pattern" Query="@State.Query" />
                          </span>
                          <span class="palette-domain">@step.Domain</span>
                          <span class="palette-used">@(step.Used)×</span>
                      </button>
                  }
                  @if (State.Results.Count == 0)
                  {
                      <div class="palette-empty" data-testid="palette-empty">
                          <p>No step matches "@State.Query".</p>
                          <p class="hint">Try a shorter query, or define a new step in the appropriate `*.Steps.cs` file.</p>
                      </div>
                  }
              </div>
              <div class="palette-footer">
                  <span><kbd>↑</kbd><kbd>↓</kbd> navigate</span>
                  <span><kbd>↵</kbd> select</span>
                  <span><kbd>Esc</kbd> close</span>
              </div>
          </div>
      </div>
  }
  ```

- **Cursor pinning on mouse move**: hovering a result sets `SelectedIndex` so keyboard-and-mouse intermixing stays predictable.

- **Header "Quick find" button**:
  - Inject `IPlatform` + `IKeyboardActions`.
  - Label: `@Platform.ShortcutLabel("K")`.
  - `@onclick="Actions.OpenPalette"`.
  - Subscribes to nothing — the action goes through the same `IKeyboardActions` seam as the keyboard shortcut.

- **`/` and `⌘K` / `Ctrl+K` already open the palette via the JS keyboard handler from US-07** — no change to the handler needed.

---

## Tasks

### Task 1 — `IPlatform` + `PlatformService` + JS helper

- [ ] `src/Delta.DocView.Client/Services/IPlatform.cs`: interface as specified above.
- [ ] `src/Delta.DocView.Client/Services/PlatformService.cs`:
    - ctor takes `IJSRuntime`.
    - `IsMac` default false.
    - `InitializeAsync()` calls `docview.platform.isMac` and sets the property. Idempotent (a `_initialized` flag).
    - `ShortcutLabel(string letter)` returns `IsMac ? $"⌘{letter}" : $"Ctrl+{letter}"`.
- [ ] Register `IPlatform -> PlatformService` as Scoped in `Program.cs`.
- [ ] Append to `src/Delta.DocView.Client/wwwroot/js/docview.js`:
    ```js
    platform: {
        isMac: function () {
            try {
                const p = navigator.platform || '';
                const ua = navigator.userAgent || '';
                return /Mac/i.test(p) || /Mac/i.test(ua);
            } catch (e) {
                return false;
            }
        }
    },
    focus: {
        _lastFocused: null,
        element: function (id) {
            try {
                this._lastFocused = document.activeElement;
                const el = document.getElementById(id);
                if (el) el.focus();
            } catch (e) { /* swallow */ }
        },
        restorePrevious: function () {
            try {
                if (this._lastFocused && typeof this._lastFocused.focus === 'function') {
                    this._lastFocused.focus();
                }
            } catch (e) { /* swallow */ }
            this._lastFocused = null;
        }
    },
    scrollIntoViewIfNeeded: function (selector) {
        try {
            const el = document.querySelector(selector);
            if (el && typeof el.scrollIntoView === 'function') {
                el.scrollIntoView({ block: 'nearest' });
            }
        } catch (e) { /* swallow */ }
    }
    ```
    Drop these as siblings to existing properties (`keyboard`, `copyText`, etc.). Keep trailing-comma style consistent with the file.
- [ ] App boot: in `src/Delta.DocView.Client/App.razor`, add `Platform.InitializeAsync()` to the existing `Task.WhenAll(LibraryClient.LoadAsync, Favourites.InitializeAsync)` so the platform flag is set before `MainLayout` renders.
- [ ] Tests in `tests/Delta.DocView.Tests/Services/PlatformServiceTests.cs`:
    - `Default_IsMac_False`.
    - `InitializeAsync_TrueFromJs_SetsIsMacTrue`.
    - `InitializeAsync_FalseFromJs_SetsIsMacFalse`.
    - `InitializeAsync_JsThrows_LeavesIsMacFalse_NoThrow` (mirror the LocalStorageFavouritesStore JSException pattern).
    - `InitializeAsync_SecondCall_NoOps`.
    - `ShortcutLabel_K_Mac_ReturnsCommandK`.
    - `ShortcutLabel_K_NonMac_ReturnsCtrlK`.

### Task 2 — `FuzzySearch`

- [ ] `src/Delta.DocView.Client/Services/FuzzySearch.cs`: static class with `int Score(string needle, string hay)` per the design notes.
- [ ] Tests in `tests/Delta.DocView.Tests/Services/FuzzySearchTests.cs`:
    - `Empty_Needle_ReturnsZero`.
    - `Exact_Substring_Match_ScoresHigh`.
    - `Subsequence_Match_Scores_NonZero` (e.g. `"log"` in `"I am logged in"`).
    - `No_Match_ReturnsZero` (e.g. `"xyz"` in `"hello"`).
    - `Case_Insensitive` (`"LOG"` in `"logged"` → > 0).
    - `Word_Boundary_Bonus` (matching after space scores higher than mid-word, given same letters).
    - `Consecutive_Match_Bonus` (`"log"` in `"login system"` scores higher than `"log"` in `"l o g out"`).
    - `Start_Of_String_Bonus` (`"l"` in `"login"` > `"l"` in `"hello"`).
    - `Ordering_Matters` (`"og"` matches `"logging"` but `"go"` does not match `"logging"`).

### Task 3 — `PaletteState`

- [ ] `src/Delta.DocView.Client/Services/PaletteState.cs`:
    - Sealed. `IDisposable`. ctor takes `ClientStepLibraryStore store`, `IKeyboardActions actions`, `SelectionState selection`.
    - Subscribes to `actions.OpenPaletteRequested` (calls `Open()`) and `actions.CloseOverlayRequested` (calls `Close()`). Unsubscribes in `Dispose`.
    - Public methods + properties per the design notes.
    - `Open()`: `IsOpen = true; SetQuery("")` (Recompute) → raises `Changed`. If already open, calling `Open()` again is a no-op.
    - `Close()`: if already closed, no-op. Otherwise reset `IsOpen=false`, `Query=""`, `Results=[]`, `SelectedIndex=0`; raise `Changed`.
    - `SetQuery(q)`: store; recompute; raise `Changed`.
    - `MoveSelectionDown()` / `Up()`: clamp at edges; raise `Changed` only on real change.
    - `SetSelectedIndex(int)`: bounds-check; raise on change.
    - `CurrentResult`: `Results.ElementAtOrDefault(SelectedIndex)`.
    - `SelectCurrent()`: if `CurrentResult is Step s`, `selection.Select(s)`; then `Close()`. Otherwise no-op.
- [ ] Register `PaletteState` as Scoped in `Program.cs`.
- [ ] Tests in `tests/Delta.DocView.Tests/Services/PaletteStateTests.cs`:
    - `Open_FromOpenPaletteRequested_OpensWithDefaultResults`: subscribe a counter to `Changed`, raise `actions.OpenPaletteRequested`; `IsOpen` true; `Results.Count > 0`; counter incremented.
    - `Close_FromCloseOverlayRequested_Closes`.
    - `Default_Results_Are_Top50_By_Used_Desc`: build a synthetic library with 60 steps of varying `Used`; open palette; assert `Results.Count == 50` and ordering descending by `Used`.
    - `SetQuery_NonEmpty_FilterByFuzzyScore`: query matches a known subset; `Results` contains only matches; ordering by score.
    - `SetQuery_NoMatch_ResultsEmpty`.
    - `MoveSelectionDown_ClampsAtLast`.
    - `MoveSelectionUp_ClampsAtFirst`.
    - `SetSelectedIndex_OutOfRange_NoChange`.
    - `SelectCurrent_WritesToSelectionState_AndCloses`.
    - `SelectCurrent_NoResults_NoOp`.
    - `Disposal_Unsubscribes_From_Actions`.

### Task 4 — `Palette.razor`

- [ ] Create `src/Delta.DocView.Client/Components/Palette.razor` per the markup design above. Injects `PaletteState`, `IJSRuntime`. `@implements IDisposable`.
- [ ] `OnAfterRenderAsync(firstRender)`: when transitioning to `IsOpen`, focus the input via `docview.focus.element(_inputId)`. Track a `_wasOpen` field for transition detection.
- [ ] When `SelectedIndex` changes, JS-scroll the active row via `docview.scrollIntoViewIfNeeded($"[data-testid='palette'] [data-result-index='{SelectedIndex}']")`.
- [ ] When `IsOpen` transitions back to false, call `docview.focus.restorePrevious()`.
- [ ] `@onkeydown` switch on `args.Key`: `"ArrowDown"`, `"ArrowUp"`, `"Enter"`, `"Escape"`. Call the matching `State` method. Call `args.PreventDefault();`? — Blazor doesn't allow setting that on the event args directly; use `@onkeydown:preventDefault` attribute selectively, OR rely on the fact that the input swallows these keys for the input's own behaviour. Acceptable: just don't `preventDefault` and let the user notice if there's a clash — in practice Arrow Up/Down and Enter are fine to handle on the input.
- [ ] CSS additions for `.palette-backdrop`, `.palette`, `.palette-input`, `.palette-meta`, `.palette-results`, `.palette-result`, `.palette-result.is-active`, `.palette-empty`, `.palette-footer`, `.palette-domain`, `.palette-used`, `.palette-pattern` — keep terse, reuse `--brand-*` tokens.
- [ ] Tests in `tests/Delta.DocView.Tests/Components/PaletteTests.cs`:
    - `Renders_Nothing_When_Closed`.
    - `Renders_Backdrop_And_Modal_When_Open`.
    - `Default_Open_Shows_TopByUsage`.
    - `Typing_In_Input_Updates_Query_And_Filters_Results`.
    - `ArrowDown_Moves_Selection`.
    - `ArrowUp_Moves_Selection_Up`.
    - `Enter_Selects_Current_And_Closes`.
    - `Escape_Closes`.
    - `Click_Result_Selects_And_Closes`.
    - `Click_Backdrop_Closes`.
    - `Empty_Results_Shows_Empty_State`.
    - `Active_Result_Has_Is_Active_Class`.

### Task 5 — Header integration

- [ ] Edit `src/Delta.DocView.Client/Layout/Header.razor`:
    - Inject `IPlatform Platform` and `IKeyboardActions Actions`.
    - Replace the static `<button class="btn-quickfind">⌘K</button>` with `<button class="btn-quickfind" data-testid="quick-find" @onclick="Actions.OpenPalette">@Platform.ShortcutLabel("K")</button>`.
- [ ] Update `tests/Delta.DocView.Tests/Components/HeaderTests.cs`:
    - Add `IPlatform` and `IKeyboardActions` to the test DI helper. Use NSubstitute for `IPlatform` to set `IsMac`.
    - `Quick_Find_Button_Shows_Command_K_On_Mac`: `platform.IsMac` returns true; assert button text contains `"⌘K"`.
    - `Quick_Find_Button_Shows_Ctrl_K_On_NonMac`: `IsMac` false; button text contains `"Ctrl+K"`.
    - `Quick_Find_Click_Opens_Palette`: click `[data-testid='quick-find']`; assert `actions.Received().OpenPalette()`.

### Task 6 — Mount + boot wiring

- [ ] Edit `src/Delta.DocView.Client/Layout/MainLayout.razor` to mount `<Palette />` once. Place it near the top of the layout root (it renders as an absolute-positioned overlay — order doesn't matter for layout).
- [ ] Verify `src/Delta.DocView.Client/App.razor` now awaits `Platform.InitializeAsync()` in `Task.WhenAll(...)` alongside library load and favourites init. (Added in Task 1; double-check.)
- [ ] Update any test setup files that render `MainLayout` (or `App` indirectly) to also register `IPlatform`, `PaletteState`, and the dependencies they need:
  - `AppTests.cs`, `MainLayoutTests.cs`, `ShellComponentTests.cs`, integration tests.
  - For `IPlatform` registration in tests, use NSubstitute returning `IsMac = false` by default.

### Task 7 — Integration coverage

- [ ] Extend `tests/Delta.DocView.Tests/Integration/FilterStackTests.cs` with end-to-end tests that mount `Header + LeftRail + StepList + DetailPanel + Palette + KeyboardHandler` in a shared `TestContext`. Use real `IKeyboardActions` and `PaletteState`; mock `IPlatform`.
- [ ] Tests:
    1. `Palette_Opens_When_Keyboard_Handler_Receives_OpenPalette`: call `keyboard.Instance.OnKey("open-palette")`; assert `[data-testid='palette']` rendered.
    2. `Palette_Opens_When_QuickFind_Clicked`: click header's `[data-testid='quick-find']`; assert palette rendered.
    3. `Palette_Typing_Filters_Results`: open palette; type into `.palette-input`; assert result count drops to matches; `data-step-id` of first result equals the highest-scoring step's id.
    4. `Palette_Enter_Selects_And_Closes`: open, navigate to a result, Enter; assert palette closed AND `SelectionState.Selected.Id` equals the selected result.
    5. `Palette_Escape_Closes_Without_Selection`: open, type query, Escape; assert palette closed AND `SelectionState.Selected` is whatever it was before (no change).
    6. `Palette_Backdrop_Click_Closes`.
    7. `Palette_Empty_Result_Shows_Hint`.
    8. `Palette_Default_Shows_Top_Usage_When_Empty_Query`: open without typing; first result is the highest-used step in the library.

---

## Out of scope (deferred)

- **Recently-used boost** (rank steps the user already selected higher in default state): worthwhile when usage telemetry exists; defer.
- **Multi-key palette commands** (e.g. `?docs` to search documentation): spec is steps-only.
- **Multiple matches highlighting** of separate fuzzy-matched characters: `PatternRenderer` highlights `Query` substring matches, which covers the obvious case; per-character fuzzy highlighting is a nice-to-have, defer.
- **Keyboard-shortcut customisation**: out of scope; addressed if/when US-11 tweaks grow.

---

## Risk + open questions

| # | Question | Owner | Decision |
|---|----------|-------|----------|
| 1 | Should `/` shortcut work even when focus is in an input? | UX | No. The global handler in US-07 skips inputs by design — typing `/` in the header search box should literally type a slash, not open the palette. This is the expected vim/quick-find convention. |
| 2 | Restore-focus on close: per-element or just blur? | UX | Restore the previously focused element via `docview.focus.restorePrevious()` so the rail filter / step-list interaction continues naturally. |
| 3 | When a domain is filtered out of the main view, should the palette include those steps? | Product | Yes — palette searches the full library regardless of the rail filters. That's the whole point of "jump to any step in two keystrokes". |
| 4 | Shortcut for non-Mac, non-Windows (Linux, ChromeOS)? | UX | `Ctrl+K`. `IPlatform.IsMac` covers the only distinct case. |
| 5 | Auto-focus reliability across browsers? | Engineering | `el.focus()` is well-supported; the JS helper swallows errors. Tests with bUnit use `JSRuntimeMode.Loose` and don't assert on actual focus — only on PaletteState transitions. |
