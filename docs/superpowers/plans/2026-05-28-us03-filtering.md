# Delta.DocView — US-03 Filtering Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a four-axis filter rail (step type · domain · param type · favourites) plus a search query, all AND-combined, that narrows the in-memory step library to a filtered set the rest of the UI will render from. Establishes the state-container pattern the remaining stories will lean on.

**Architecture:** A single `Scoped` `FilterState` service holds the active selection and the search query, and exposes an `OnChanged` event. A pure static `FilterEngine.Apply(...)` returns the filtered set; consumers subscribe to `FilterState.OnChanged` and re-render. Favourites are encapsulated behind `IFavouritesStore` — this story ships an in-memory implementation; US-05 replaces it with the `localStorage` variant without touching call sites.

**Tech stack additions:** none. Pure C# + Blazor + existing bUnit/xUnit/NSubstitute.

---

## File Map

```
src/Delta.DocView.Client/
  Services/
    FilterState.cs              ← scoped state container w/ OnChanged event
    FilterEngine.cs             ← pure static Apply(steps, state, query)
    IFavouritesStore.cs         ← Toggle/Has/Count/All + Changed event
    InMemoryFavouritesStore.cs  ← v1 implementation (US-05 swaps for localStorage)
    DomainPalette.cs            ← stable id → hue mapping for --dom-{id} vars
  Components/
    StepTypeFilter.razor        ← Given/When/Then/And toggle row + counts
    DomainFilter.razor          ← All + per-domain rows w/ dots + counts
    ParamTypeFilter.razor       ← chip grid w/ multi-select
    FavouritesToggle.razor      ← star toggle + count badge
    DomainStyles.razor          ← emits <style> with --dom-{id} CSS vars (root)
  Layout/
    LeftRail.razor              ← UPDATED: composes the four filter components
    Header.razor                ← UPDATED: search input bound to FilterState.Query (closes TD-14)
    MainLayout.razor            ← UPDATED: hosts DomainStyles once
  Program.cs                    ← UPDATED: register FilterState, IFavouritesStore

tests/Delta.DocView.Tests/
  Services/
    FilterEngineTests.cs        ← isolation + combinations + edge cases
    FilterStateTests.cs         ← event raises, query/setter behaviour
    InMemoryFavouritesStoreTests.cs
  Components/
    StepTypeFilterTests.cs      ← guard "at least one selected", count rendering
    DomainFilterTests.cs        ← All clears domain, single select
    ParamTypeFilterTests.cs     ← multi-select, deselect-all = no filter
    FavouritesToggleTests.cs    ← active state, count updates
```

---

## Design notes

- **FilterState** properties: `ISet<string> Types` (default `{Given, When, Then, And}`), `string? Domain` (null = All), `ISet<string> ParamTypes` (empty = no filter), `bool FavsOnly`, `string Query` (empty = no filter). Exposes `event Action? OnChanged` and a single `NotifyChanged()` raised by each setter / mutator.
- **At-least-one type** invariant lives in `FilterState.ToggleType(string)` — the setter rejects the deselect that would empty the set. Component just calls the setter.
- **Counts** rendered in filter rows are **total** counts from the loaded library — independent of other active filters. (Per spec wording: "All domains N" and per-type badges read as static totals; we can revisit if usability testing wants context-aware counts.)
- **Domain colours**: `DomainPalette.HueFor(id)` returns a deterministic hue from a stable hash of the domain id; `DomainStyles.razor` emits one `<style>` block at MainLayout root: `:root { --dom-auth: hsl(170 60% 45%); --dom-billing: ... }`. CSS already references `--dom-{id}`; we just need to define them once at boot.
- **Search query** is owned by `FilterState`, bound from Header's search input via `@bind-Value:event="oninput"`. Closes TD-14 as a side-effect of US-03.
- **Filter persistence**: spec says session-only — no localStorage. Lives only in the Scoped service.
- **FilterEngine.Apply** signature: `static IReadOnlyList<Step> Apply(IEnumerable<Step> steps, FilterState state, IFavouritesStore favs)` — query lives on state so the engine has one input source. Returns a list (concrete) so groupers in US-04 can enumerate twice.

---

## Tasks

### Task 1 — Add `FilterState`, `IFavouritesStore`, `InMemoryFavouritesStore`, register in DI
- [ ] Create `FilterState.cs` with properties + `OnChanged` event + mutators (`ToggleType`, `SetDomain`, `ToggleParamType`, `SetFavsOnly`, `SetQuery`). Each mutator calls `NotifyChanged()` only if value actually changed.
- [ ] Enforce at-least-one-type invariant inside `ToggleType`.
- [ ] Create `IFavouritesStore` with `bool Has(string id)`, `void Toggle(string id)`, `int Count`, `IReadOnlyCollection<string> All`, `event Action? Changed`.
- [ ] Create `InMemoryFavouritesStore` implementing the interface.
- [ ] `Program.cs`: register both as `Scoped`.
- [ ] `FilterStateTests`: ToggleType cannot empty the set; setters raise `OnChanged` only on real change; SetQuery normalises null → "".
- [ ] `InMemoryFavouritesStoreTests`: Toggle adds/removes; Count reflects; Changed raised.

### Task 2 — `FilterEngine.Apply`
- [ ] Create `FilterEngine.cs` static class.
- [ ] Apply order: Types → Domain → ParamTypes → FavsOnly → Query substring (case-insensitive, matches `step.Pattern`).
- [ ] `FilterEngineTests` (TDD-first):
    - all types selected + empty everything → returns full library
    - single type → only matching type
    - domain set → only that domain
    - param type set → steps whose params contain at least one matching type
    - param type with no matches → empty
    - favs-only with no favourites → empty
    - query substring matches pattern, case-insensitive
    - combined filters AND together correctly

### Task 3 — `DomainPalette` + `DomainStyles.razor`
- [ ] `DomainPalette.HueFor(string id)` — deterministic int hash → `hue ∈ [0,360)`.
- [ ] `DomainStyles.razor` reads `ClientStepLibraryStore.Domains` and renders `<style>:root { --dom-{id}: hsl(h 60% 45%); ... }</style>` at first render.
- [ ] Place `<DomainStyles />` once in `MainLayout.razor` (above app-body).
- [ ] Unit test: `DomainPalette.HueFor` returns same value for same id; spread across hue space for several inputs.

### Task 4 — `StepTypeFilter.razor`
- [ ] Inject `FilterState`, `ClientStepLibraryStore`. Subscribe to `OnChanged` in `OnInitialized`; unsubscribe in `Dispose`.
- [ ] Render four buttons (Given/When/Then/And) with `is-active` class when in `state.Types`. Count badge shows total per type from the library.
- [ ] Click calls `state.ToggleType(type)`.
- [ ] bUnit test: clicking a selected last-active button leaves state unchanged (guard); count badges render correctly.

### Task 5 — `DomainFilter.razor`
- [ ] Inject `FilterState`, `ClientStepLibraryStore`. Subscribe / dispose.
- [ ] Render "All domains N" button (active when `state.Domain is null`) + one row per domain (label, coloured dot via `style="background:var(--dom-{id})"`, count). Selecting "All" calls `state.SetDomain(null)`; selecting a domain calls `state.SetDomain(id)`.
- [ ] bUnit test: domains list rendered, click changes state, "All" clears domain.

### Task 6 — `ParamTypeFilter.razor`
- [ ] Inject `FilterState`, `ClientStepLibraryStore`. Subscribe / dispose.
- [ ] Render chips for distinct param types from `library.Steps.SelectMany(s => s.Params.Select(p => p.Type)).Distinct()`. Multi-select; chip is active when in `state.ParamTypes`.
- [ ] bUnit test: chips rendered from data, toggling adds/removes from state, all-deselected leaves filter empty.

### Task 7 — `FavouritesToggle.razor`
- [ ] Inject `FilterState`, `IFavouritesStore`. Subscribe to both events; dispose.
- [ ] Render star icon + count badge. `is-active` when `state.FavsOnly`. Click → `state.SetFavsOnly(!state.FavsOnly)`.
- [ ] bUnit test: count reflects store; click toggles state.

### Task 8 — Wire LeftRail + Header search
- [ ] `LeftRail.razor`: stack the four filter components in spec order.
- [ ] `Header.razor`: add `<input @bind-Value="query" @bind-Value:event="oninput" />` mapped to `FilterState.Query`. Inject `FilterState` and subscribe so external resets reflect. (Closes [TD-14](https://github.com/tafallen/Delta.DocView/issues/27).)
- [ ] bUnit: typing in header input updates `FilterState.Query`.

### Task 9 — Smoke + commit hygiene
- [ ] `dotnet build` clean; `dotnet test` green (target: ≥ 70 tests total).
- [ ] `dotnet run --project src/Delta.DocView.Server` — manually verify in browser that filter UI renders and search input is wired (no list yet — that lands in US-04).
- [ ] One commit per task, each `refs #3` (US-03 issue), final commit `Closes #3` + `Closes #27` (TD-14).

---

## Out of scope (deferred)

- Applying `FilterEngine` output to the visible step list — that's US-04.
- Persisting favourites in `localStorage` — that's US-05; this story ships only the in-memory store behind the interface.
- Context-aware counts (counts that update with other filters) — explicitly per-spec totals are static for v1; revisit if usability testing flags it.

---

## Risk + open questions

| # | Question | Owner | Decision |
|---|----------|-------|----------|
| 1 | Should filter counts update as other filters apply (context-aware) or stay total? | Product | Defaulting to static totals (matches "All domains N" reading literally). Easy to change later. |
| 2 | Search query position — header (TD-14) or also a Command Palette concern (US-08)? | Engineering | Header input drives `FilterState.Query` now; US-08 palette is separate. |
| 3 | Domain palette — hand-curated or hashed? | Design | Hashed default in v1; can override with explicit map later without touching consumers. |
