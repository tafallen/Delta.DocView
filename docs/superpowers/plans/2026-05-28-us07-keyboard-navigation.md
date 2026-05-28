# Delta.DocView — US-07 Keyboard Navigation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans`. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Let a power user drive the viewer from the keyboard — `J`/`K` move selection through the filtered list, `F` toggles favourite on the selection, `⌘K`/`/`/`?`/`C` trigger the overlays that US-08/09/10 will build, and `Escape` closes any open overlay. No firing while the user is typing in an input or contenteditable.

**Architecture:**

- A JS handler `docview.keyboard.attach(dotnetRef)` listens to `window` `keydown`. It short-circuits when `document.activeElement` is an `<input>`, `<textarea>`, or `[contenteditable]`. For relevant keys it invokes a single `[JSInvokable]` method `OnKey(string action)` on the `KeyboardHandler.razor` component.
- `KeyboardHandler.razor` mounts once inside `MainLayout` and forwards the action name to a Scoped `IKeyboardActions` service. The component owns the JS attach/detach lifecycle.
- `IKeyboardActions` is the single seam every keyboard action funnels through. For actions whose consumer exists today (`SelectNext/Prev`, `ToggleSelectedFavourite`, `CloseOverlay`) the impl does the work directly. For actions whose consumers land in US-08/09/10 (`OpenPalette`, `OpenShortcuts`, `ToggleComposer`) it raises a public event the future state object will subscribe to — keeping the dispatch layer closed for modification.
- `J`/`K` nav needs the filtered+sorted list. Lift the projection out of `StepList` into a Scoped `FilteredStepsProvider` that subscribes to `FilterState.Changed` and `IFavouritesStore.Changed`, recomputes, and raises its own `Changed` event. `StepList` becomes a consumer; `IKeyboardActions` reads the same list.
- Edge cases:
  - `J` with no selection → select the first filtered step.
  - `K` with no selection → no-op (don't surprise the user by jumping to the bottom).
  - `J`/`K` at the edges → clamp; no wraparound.
  - Selection on a step that's no longer in the filtered list (filters changed underneath) → `J` moves to first filtered step; `K` no-op.
  - `F` with no selection → no-op.
  - `Escape` with nothing open → no-op (subscribers handle it).

**Tech stack additions:** none.

---

## File Map

```
src/Delta.DocView.Client/
  Services/
    FilteredStepsProvider.cs       ← new (Scoped projection service)
    IKeyboardActions.cs            ← new (interface)
    KeyboardActions.cs             ← new (impl)
  Components/
    StepList.razor                 ← UPDATED to consume FilteredStepsProvider
    KeyboardHandler.razor          ← new (mounts JS, forwards OnKey)
  Layout/
    MainLayout.razor               ← UPDATED — mounts <KeyboardHandler />
  Program.cs                       ← UPDATED — register the three new services
  wwwroot/js/docview.js            ← UPDATED — keyboard.attach/detach + active-element check

tests/Delta.DocView.Tests/
  Services/
    FilteredStepsProviderTests.cs
    KeyboardActionsTests.cs
  Components/
    KeyboardHandlerTests.cs
  Integration/
    FilterStackTests.cs            ← EXTENDED — J/K/F/Escape end-to-end
```

---

## Design notes

- **`IKeyboardActions` surface**:
  ```csharp
  public interface IKeyboardActions
  {
      // wired in US-07
      void SelectNext();
      void SelectPrev();
      void ToggleSelectedFavourite();
      void CloseOverlay();      // raises CloseOverlayRequested event

      // stubs in US-07; consumers land in US-08/09/10
      void OpenPalette();       // raises OpenPaletteRequested event
      void OpenShortcuts();     // raises OpenShortcutsRequested event
      void ToggleComposer();    // raises ToggleComposerRequested event

      // events the above stub methods raise — subscribed by future state objects
      event Action? OpenPaletteRequested;
      event Action? OpenShortcutsRequested;
      event Action? ToggleComposerRequested;
      event Action? CloseOverlayRequested;
  }
  ```
  Direct-method actions own behaviour locally; event-fired actions stay open for US-08/09/10 to wire without touching the dispatch layer.
- **Action name strings** sent across the JS → .NET boundary:
  - `"select-next"` (J)
  - `"select-prev"` (K)
  - `"toggle-fav"` (F)
  - `"close-overlay"` (Escape)
  - `"open-palette"` (⌘K, /)
  - `"open-shortcuts"` (?)
  - `"toggle-composer"` (C)
  Lifted to a `KeyboardActionNames` static class so the strings live in one place; tests can reference them by name without typo risk.
- **`FilteredStepsProvider`**: replicates the projection that lives inline in `StepList.razor` today. Scoped. Constructor takes `ClientStepLibraryStore`, `FilterState`, `IFavouritesStore`; subscribes to `FilterState.Changed` and `Favs.Changed`; recomputes `Filtered` (via `FilterEngine.Apply` then `StepRanking.Rank`); raises its own `Changed` event. Disposable to unsubscribe. `StepList.razor` injects it instead of computing inline; tests for StepList that previously populated FilterState observe the same renders.
- **JS active-element check**:
  ```js
  const a = document.activeElement;
  if (a && (a.tagName === 'INPUT' || a.tagName === 'TEXTAREA' || a.isContentEditable)) return;
  ```
  Applies to every relevant key. The header search input is exempt because the user's typing in it — `/` to focus the search box later is fine because the focus moves into the input *after* the dispatch; we're not focusing it here.
- **Key → action mapping in JS** (lowercased letters, ignore Shift except for `?`):
  ```js
  const key = e.key;
  const lower = key.toLowerCase();
  if ((e.ctrlKey || e.metaKey) && lower === 'k') return invoke('open-palette');
  if (key === '/') return invoke('open-palette');
  if (key === '?') return invoke('open-shortcuts');
  if (lower === 'c' && !e.ctrlKey && !e.metaKey && !e.altKey) return invoke('toggle-composer');
  if (lower === 'f' && !e.ctrlKey && !e.metaKey && !e.altKey) return invoke('toggle-fav');
  if (lower === 'j' && !e.ctrlKey && !e.metaKey && !e.altKey) return invoke('select-next');
  if (lower === 'k' && !e.ctrlKey && !e.metaKey && !e.altKey) return invoke('select-prev');
  if (key === 'Escape') return invoke('close-overlay');
  ```
  `preventDefault` is called only on the keys we handle (so `/` doesn't trigger Firefox's quick-find, `?` doesn't bring up browser help, etc.).
- **`KeyboardHandler.razor`** is a near-empty render (no DOM); it exists for the lifecycle hooks. Mount it once at the top of `MainLayout.razor`. The handler holds a `DotNetObjectReference<KeyboardHandler>` that is created in `OnAfterRenderAsync(firstRender: true)` and disposed in `Dispose`.

---

## Tasks

### Task 1 — Extract `FilteredStepsProvider`

- [ ] Create `src/Delta.DocView.Client/Services/FilteredStepsProvider.cs`:
  - Sealed class. Implements `IDisposable`.
  - Constructor takes `ClientStepLibraryStore`, `FilterState`, `IFavouritesStore`. Subscribes to `FilterState.Changed` and `IFavouritesStore.Changed`. Computes initial `Filtered`.
  - Public: `IReadOnlyList<Step> Filtered { get; private set; }`, `event Action? Changed`.
  - Private `Recompute()` that runs `FilterEngine.Apply(...)` then `StepRanking.Rank(...)`; raises `Changed` only when the list reference changes (compare by `SequenceEqual`? Too expensive — just raise after every recompute; subscribers `StateHasChanged` cheaply).
- [ ] Register as Scoped in `Program.cs`.
- [ ] Refactor `src/Delta.DocView.Client/Components/StepList.razor`:
  - Inject `FilteredStepsProvider` instead of running the projection inline.
  - Subscribe to `FilteredStepsProvider.Changed`; remove direct subscriptions to `FilterState.Changed` and `Favs.Changed` (the provider handles them now).
  - Read `_filtered` from `Provider.Filtered`.
- [ ] Tests in `tests/Delta.DocView.Tests/Services/FilteredStepsProviderTests.cs`:
  - Initial state: provider's `Filtered` matches `FilterEngine.Apply(Store.Steps, default state, empty favs)` ranked by `StepRanking`.
  - Toggling a filter raises `Changed` and updates `Filtered`.
  - Toggling a favourite raises `Changed`.
  - Disposal unsubscribes (subsequent FilterState changes no longer raise — verify by capturing event counter).
- [ ] Existing `StepListTests` continue passing — they exercise the rendered DOM; behaviour unchanged.

### Task 2 — `IKeyboardActions` + `KeyboardActions` impl + `KeyboardActionNames`

- [ ] Create `src/Delta.DocView.Client/Services/IKeyboardActions.cs` (interface as designed above).
- [ ] Create `src/Delta.DocView.Client/Services/KeyboardActions.cs`:
  - Constructor takes `SelectionState`, `IFavouritesStore`, `FilteredStepsProvider`.
  - `SelectNext()`: looks at `Provider.Filtered` and `Selection.Selected`. Clamping logic per the design-notes edge cases.
  - `SelectPrev()`: mirror logic.
  - `ToggleSelectedFavourite()`: if `Selection.Selected is not null`, `Favs.Toggle(Selection.Selected.Id)`. Otherwise no-op.
  - `OpenPalette`, `OpenShortcuts`, `ToggleComposer`, `CloseOverlay`: raise the corresponding events.
- [ ] Create `src/Delta.DocView.Client/Services/KeyboardActionNames.cs` (or a `public static class KeyboardActionNames` inside the same file as `IKeyboardActions`) — string constants for the 7 actions.
- [ ] Register `IKeyboardActions -> KeyboardActions` as Scoped in `Program.cs`.
- [ ] Tests in `tests/Delta.DocView.Tests/Services/KeyboardActionsTests.cs`:
  - `SelectNext_WithNoSelection_SelectsFirstFiltered`.
  - `SelectNext_AtLastItem_NoOp`.
  - `SelectNext_FromMiddle_MovesForward`.
  - `SelectPrev_WithNoSelection_NoOp` (don't surprise the user).
  - `SelectPrev_AtFirstItem_NoOp`.
  - `SelectPrev_FromMiddle_MovesBackward`.
  - `SelectNext_FromSelectionNotInFilteredList_SelectsFirstFiltered`.
  - `ToggleSelectedFavourite_WithSelection_TogglesById`.
  - `ToggleSelectedFavourite_NoSelection_NoOp` (and `Favs.Count == 0` after).
  - `OpenPalette_RaisesOpenPaletteRequested`.
  - `OpenShortcuts_RaisesOpenShortcutsRequested`.
  - `ToggleComposer_RaisesToggleComposerRequested`.
  - `CloseOverlay_RaisesCloseOverlayRequested`.

### Task 3 — JS `docview.keyboard` helpers

- [ ] Append to `src/Delta.DocView.Client/wwwroot/js/docview.js`:
  ```js
  keyboard: {
      _ref: null,
      _handler: null,

      attach: function (dotnetRef) {
          if (this._handler) return; // idempotent
          this._ref = dotnetRef;
          this._handler = (e) => {
              const a = document.activeElement;
              if (a && (a.tagName === 'INPUT' || a.tagName === 'TEXTAREA' || a.isContentEditable)) return;

              const key = e.key;
              const lower = key.length === 1 ? key.toLowerCase() : key;
              let action = null;

              if ((e.ctrlKey || e.metaKey) && lower === 'k') action = 'open-palette';
              else if (key === '/') action = 'open-palette';
              else if (key === '?') action = 'open-shortcuts';
              else if (lower === 'c' && !e.ctrlKey && !e.metaKey && !e.altKey) action = 'toggle-composer';
              else if (lower === 'f' && !e.ctrlKey && !e.metaKey && !e.altKey) action = 'toggle-fav';
              else if (lower === 'j' && !e.ctrlKey && !e.metaKey && !e.altKey) action = 'select-next';
              else if (lower === 'k' && !e.ctrlKey && !e.metaKey && !e.altKey) action = 'select-prev';
              else if (key === 'Escape') action = 'close-overlay';

              if (action) {
                  e.preventDefault();
                  this._ref.invokeMethodAsync('OnKey', action);
              }
          };
          window.addEventListener('keydown', this._handler);
      },

      detach: function () {
          if (this._handler) {
              window.removeEventListener('keydown', this._handler);
              this._handler = null;
              this._ref = null;
          }
      }
  }
  ```
- [ ] No new JS-side tests; behaviour is asserted on the .NET side via `KeyboardHandler` + integration tests.

### Task 4 — `KeyboardHandler.razor` + mount in MainLayout

- [ ] Create `src/Delta.DocView.Client/Components/KeyboardHandler.razor`:
  ```razor
  @inject IJSRuntime JS
  @inject IKeyboardActions Actions
  @implements IAsyncDisposable

  @code {
      private DotNetObjectReference<KeyboardHandler>? _ref;

      protected override async Task OnAfterRenderAsync(bool firstRender)
      {
          if (!firstRender) return;
          _ref = DotNetObjectReference.Create(this);
          await JS.InvokeVoidAsync("docview.keyboard.attach", _ref);
      }

      [JSInvokable]
      public void OnKey(string action)
      {
          switch (action)
          {
              case KeyboardActionNames.SelectNext:      Actions.SelectNext(); break;
              case KeyboardActionNames.SelectPrev:      Actions.SelectPrev(); break;
              case KeyboardActionNames.ToggleFav:       Actions.ToggleSelectedFavourite(); break;
              case KeyboardActionNames.OpenPalette:     Actions.OpenPalette(); break;
              case KeyboardActionNames.OpenShortcuts:   Actions.OpenShortcuts(); break;
              case KeyboardActionNames.ToggleComposer:  Actions.ToggleComposer(); break;
              case KeyboardActionNames.CloseOverlay:    Actions.CloseOverlay(); break;
          }
      }

      public async ValueTask DisposeAsync()
      {
          try { await JS.InvokeVoidAsync("docview.keyboard.detach"); } catch { }
          _ref?.Dispose();
      }
  }
  ```
  The component renders no DOM.
- [ ] Mount `<KeyboardHandler />` at the top of `src/Delta.DocView.Client/Layout/MainLayout.razor`, just inside the root.
- [ ] Tests in `tests/Delta.DocView.Tests/Components/KeyboardHandlerTests.cs`:
  - `OnKey_Dispatches_SelectNext_To_Actions`: NSubstitute mock `IKeyboardActions`; render the component; call `cut.Instance.OnKey("select-next")`; verify `actions.Received().SelectNext()`.
  - One test per action (7 total) — short and mechanical.
  - `Unknown_Action_NoOp`: `cut.Instance.OnKey("bogus")`; verify no method called on the mock.

### Task 5 — Integration coverage

- [ ] Extend `tests/Delta.DocView.Tests/Integration/FilterStackTests.cs` with end-to-end tests that render `<KeyboardHandler />` alongside `<Header />`, `<LeftRail />`, `<StepList />`, `<DetailPanel />` in the same `TestContext`. The `KeyboardHandler` is the unit under test; we don't trigger JS — we call the component's `[JSInvokable]` method directly.
  1. `J_With_No_Selection_Selects_First_Filtered`: assert `SelectionState.Selected.Id` is the first row's id after `keyboardHandler.Instance.OnKey("select-next")`.
  2. `J_Then_J_Moves_To_Second_Row`.
  3. `K_From_First_Item_Is_NoOp`.
  4. `J_At_Last_Item_Is_NoOp_NoWrap`.
  5. `F_With_Selection_Toggles_Favourite`: select a step; call `OnKey("toggle-fav")`; assert `Favs.Has(selectedId)` flips. Detail panel's favourite button should now have `is-fav`.
  6. `F_With_No_Selection_Does_Not_Throw_And_Does_Not_Toggle_Anything`.
  7. `OpenPalette_Raises_Event`: subscribe a counter to `actions.OpenPaletteRequested`; call `OnKey("open-palette")`; assert counter == 1. (Same template for `OpenShortcuts`, `ToggleComposer`, `CloseOverlay`.)
  8. Filtered-list change while selection is held: select step A; toggle FavsOnly such that A is filtered out; call `OnKey("select-next")`; assert the new selection is the first item of the now-filtered list.
- [ ] No new CSS — the keyboard handler renders nothing.

---

## Out of scope (deferred)

- **Palette UI** (US-08): `OpenPaletteRequested` fires but has no subscriber until US-08 lands.
- **Composer UI** (US-09): `ToggleComposerRequested` fires; subscriber lands with the composer.
- **Shortcuts overlay** (US-10): `OpenShortcutsRequested` fires; subscriber lands with the overlay. The overlay's content (the printable list of all shortcuts) is US-10's responsibility — US-07 only provides the trigger.
- **Vim-style g/G** (go to first / last): not in spec; defer until requested.
- **Tab focus management** between filter rail, list, and detail: separate concern; not in US-07 AC.

---

## Risk + open questions

| # | Question | Owner | Decision |
|---|----------|-------|----------|
| 1 | Wrap on J/K at edges, or clamp? | UX | Clamp — wrap surprises users and obscures that there's an end. |
| 2 | What about Caps Lock or Shift held while pressing F/J/K? | UX | Accept both cases (lowercase in JS via `key.toLowerCase()`); spec writes uppercase but the lowercase ergonomics are the de-facto vim convention. |
| 3 | Should the selected row auto-scroll into view on J/K nav? | UX | Yes — but the scroll-into-view JS is a small follow-up to the selection write. Defer to a TD ticket post-merge if it isn't trivially achievable in this story; add as Task 5b only if it's a 10-minute fix. |
| 4 | `KeyboardCoordinator`-style bus vs. direct `IKeyboardActions` dispatch? | Engineering | Direct dispatch with events for not-yet-implemented consumers. Bus only if a third subscriber per action appears; YAGNI for now. |
| 5 | Does the keyboard handler block when a step is selected but the filtered list is empty (filters exclude the selection's step)? | Engineering | `J` falls back to "select first filtered step"; if filtered is also empty, no-op. Same for `K`. |
