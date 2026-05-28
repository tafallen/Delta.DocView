# Delta.DocView — User Stories

**Product**: SpecFlow Step Library documentation viewer  
**Tech stack**: .NET 8 / Blazor Server, Docker, Entra ID (dev bypass), SQLite/EF Core optional  
**Data source**: `step-library.v1.schema.json` (loaded at startup, validated against schema)  
**Schema version**: `step-library.v1`

---

## Problem Statement

QA engineers and developers writing SpecFlow feature files have no fast way to discover, browse, or compose steps from the 2,000+ step library. They copy patterns from memory or search across C# files, wasting time and producing inconsistent scenarios. This viewer gives the whole team a searchable, filterable reference with a scenario composer that outputs ready-to-paste Gherkin.

---

## Goals

1. Any team member can find the right SpecFlow step in under 10 seconds.
2. A user can compose a complete Gherkin scenario without leaving the viewer.
3. The viewer deploys as a Docker image; authenticated access is enforced in production via Entra ID.
4. Dark mode, density, and accent preferences persist across sessions.
5. The library file can be replaced without rebuilding the image (mount or volume).

---

## Non-Goals (v1)

- **Editing steps** — this is a read-only reference viewer, not an authoring tool.
- **Multiple library files** — a single `step-library.v1.json` file is loaded per instance.
- **Full-text semantic search** — keyword/fuzzy matching only; no HNSW/embedding search.
- **User accounts / saved scenarios** — the composer is ephemeral (in-browser state only).
- **CI integration / badge reporting** — out of scope; may follow in v2.

---

## Stories

---

### US-01 · Library loading and validation

**As a** site operator,  
**I want** the application to load and validate the step library JSON at startup,  
**so that** a corrupt or schema-invalid file is detected immediately with a clear error, not silently ignored.

#### Acceptance criteria

- [ ] The app reads the library file path from `DOCVIEW_LIBRARY_PATH` env var (default: `data/step-library.json`).
- [ ] On startup the file is deserialised and validated against the `step-library.v1.schema.json` schema.
- [ ] If the file is missing, a startup error page shows the expected path.
- [ ] If the file fails schema validation, the error page lists the first 5 validation errors.
- [ ] If the file passes validation, `steps` count, `version`, and `generatedAt` are logged at `Information` level.
- [ ] The SHA-256 `signature.digest` in the file is verified against the file content (excluding the `signature` property); a mismatch shows a warning banner but still loads.
- [ ] Restarting the container with a valid file recovers the app.

#### Implementation tasks

1. `StepLibraryLoader` service — reads and deserialises JSON using `System.Text.Json`.
2. `StepLibraryValidator` — validates against the bundled schema using `JsonSchema.Net`.
3. `SignatureVerifier` — computes SHA-256 of the content-without-signature property and compares.
4. Startup check registered in `Program.cs`; on failure writes to a static `IStartupError` singleton.
5. `ErrorPage.razor` — rendered when `IStartupError.HasError` is true.
6. Unit tests: missing file, invalid schema, bad signature, valid file.

---

### US-02 · Three-panel layout with header

**As a** developer browsing the step library,  
**I want** a three-column layout (filter rail · step list · detail panel) beneath a persistent header,  
**so that** I can filter, scan, and inspect steps without losing context.

#### Acceptance criteria

- [ ] Header spans full width; contains: logo mark + "Delta · Step Library" title + subtitle (step count, library version), a search input, a "Quick find" button showing `⌘K`, a shortcuts icon button, a theme-toggle icon button, and a user avatar chip.
- [ ] Left rail is ~240 px wide and contains filter sections (see US-03).
- [ ] Center list takes remaining flex space up to ~420 px; right detail fills the rest.
- [ ] Layout is responsive: on viewports < 900 px the detail panel hides; < 600 px the rail also hides.
- [ ] Dark mode (`data-dark="true"` on root) applies CSS variables across all three panels including the header.

#### Implementation tasks

1. `MainLayout.razor` with CSS Grid/Flexbox three-column structure.
2. `Header.razor` component.
3. `LeftRail.razor` shell (content filled by US-03).
4. `StepList.razor` shell (content filled by US-04).
5. `DetailPanel.razor` shell (content filled by US-06).
6. CSS custom properties file: accent colours (teal/amber/violet), density tokens, dark-mode palette.
7. Snapshot/visual tests: light and dark at 1440 × 900.

---

### US-03 · Filtering — step type, domain, parameter type, favourites

**As a** QA engineer,  
**I want** to filter the step list by step type, domain, parameter type, and favourites,  
**so that** I can narrow 2,000+ steps to the handful relevant to my current task.

#### Acceptance criteria

- [x] **Step type** row shows four toggle buttons: Given · When · Then · And, each with a count badge. At least one type must remain selected (toggle is blocked if it would deselect all).
- [x] **Domain** section shows "All domains N" plus one button per domain, each with a coloured dot and count. Selecting a domain activates it; selecting "All" clears domain filter.
- [x] **Parameter type** grid shows `string`, `int`, `decimal`, `DocString` chips; multi-select; selects only steps that have at least one param of that type. Deselecting all = no filter.
- [x] **Favourites** toggle button shows a star icon and count; when active shows only favourited steps.
- [x] Filters are AND-combined: type filter AND domain filter AND param filter AND favourites filter AND search query.
- [x] Filter state is not persisted between page loads (session-only).
- [x] Domain colour dots match `--dom-{domainid}` CSS variables (lower-case domain id).

#### Implementation tasks

1. `FilterState` record (or service) holding: `ISet<string> Types`, `string? Domain`, `ISet<string> ParamTypes`, `bool FavsOnly`.
2. `StepTypeFilter.razor` — four toggle buttons with counts.
3. `DomainFilter.razor` — domain list built from `step.domain` values in the library.
4. `ParamTypeFilter.razor` — chips from distinct param types in library.
5. `FavouritesToggle.razor`.
6. `FilterEngine` — static method `Apply(IEnumerable<Step>, FilterState, string query)`.
7. Unit tests for `FilterEngine`: each filter in isolation, combinations, edge cases (all off blocked).

---

### US-04 · Step list with grouping, counts, and search

**As a** developer,  
**I want** the step list to display steps grouped by domain with real-time search filtering and usage-sorted order,  
**so that** I can find the most relevant steps quickly.

#### Acceptance criteria

- [x] List header shows `N of M matching` and the active query in quotes when a query is set.
- [x] "Sorted by most used" button is present (v1: display only, no alternative sort required).
- [x] When "All domains" is selected, steps are grouped by domain; each group has a coloured dot, domain label, and count. Groups are ordered by descending step count.
- [x] When a specific domain is selected, no grouping header is shown (flat list).
- [x] Each row shows: type chip (Given/When/Then/And colour-coded), step pattern with `{param : type}` highlighted, file path + line number, up to 3 tags, usage count.
- [x] The active query highlights matching substrings in the pattern text.
- [x] Each row has a star (favourite) button and a `+` (add to composer) button; both fire without selecting the row. *— `+` button is rendered disabled with "coming soon" tooltip; wired in US-09.*
- [x] Clicking a row selects it and opens its detail in the right panel. *— click writes to SelectionState; detail panel content lands in US-06.*
- [x] Empty state: "No steps match your filters." with hint text when filtered list is empty.
- [x] List scrolls independently of the header and rail.

#### Implementation tasks

1. `StepRow.razor` component: type chip, pattern renderer, meta line, action buttons.
2. `PatternRenderer.razor` — splits pattern text on `{name}` tokens, renders each with typed pill.
3. `HighlightRenderer.razor` — wraps matching substring in `<mark>`.
4. `GroupedList.razor` — renders domain groups or flat list.
5. `StepList.razor` — binds to `FilterEngine` output, passes selection to parent via `EventCallback<Step>`.
6. Integration test: given a loaded library, filtering by type/domain returns expected subset.

---

### US-05 · Step favourites (persistent)

**As a** QA engineer,  
**I want** to mark steps as favourites and have that preference survive a page refresh,  
**so that** I can maintain a personal shortlist without re-discovering them each session.

#### Acceptance criteria

- [x] Clicking the star on a row or detail panel toggles favourite status.
- [x] Favourites are stored in `localStorage` under key `docview.favs.v1` as a JSON array of step IDs.
- [x] On load, favourites are restored from `localStorage`.
- [ ] Keyboard shortcut `F` while a step is selected toggles its favourite status. *— deferred to US-07 per implementation-task split.*
- [x] The favourites count in the rail filter button updates immediately.

#### Implementation tasks

1. `FavouritesStore` JS-interop service — wraps `localStorage.getItem/setItem`.
2. `FavouritesStore` is injected into `MainLayout`; exposes `Toggle(string id)`, `Has(string id)`, `Count`.
3. On `OnAfterRenderAsync(firstRender: true)` call `LoadAsync()`.
4. Unit test (bUnit): toggling adds/removes ID; restores from injected localStorage mock.

---

### US-06 · Step detail panel

**As a** developer,  
**I want** to see full details of a selected step — description, parameters, "try it" composer, C# source, and suggested next steps —  
**so that** I can understand exactly how to use it before adding it to a scenario.

#### Acceptance criteria

- [ ] Header row: type chip, domain dot + label, Favourite button, "Add to scenario" primary button.
- [ ] Pattern rendered large with typed `{param : type}` pills.
- [ ] Description paragraph below the pattern.
- [ ] Stats strip: "Used in N scenarios", "Source file:line", "Tags" (joined with ·).
- [ ] **Parameters table** (hidden when no params): columns Name / Type / Example.
- [ ] **Try it** section: for each param, an editable input pre-filled with `example`; live "Composed line" preview updates as values change; Copy button copies the composed line.
- [ ] **Try it** section when no params: shows the final line immediately with a Copy button.
- [ ] Copy shows a transient "✓ line copied" confirmation for 1.1 s.
- [ ] **C# step definition** collapsible: collapsed by default (configurable via tweaks); toggle arrow; shows syntax-highlighted C# source; "Copy source" button when expanded.
- [ ] **Likely next steps** section (hidden when empty): up to 4 related step cards, each clickable to navigate to that step.
- [ ] When no step is selected the panel is empty/blank.

#### Implementation tasks

1. `DetailPanel.razor` — receives `Step? Selected` parameter.
2. `PatternHeading.razor` — large pattern with typed pill renderer.
3. `StatsStrip.razor` — three-cell strip.
4. `ParamsTable.razor` — table with type-coloured chips.
5. `TryItSection.razor` — input-per-param, live-composed line via `@bind`, copy via JSInterop.
6. `CSharpBlock.razor` — syntax highlighted code block (server-side highlight via simple token pass; no external lib required).
7. `RelatedSteps.razor` — up to 4 clickable cards using `suggestsNext` IDs resolved from the library.
8. Unit tests (bUnit): Try-it composes correct line; related cards resolved correctly; empty state when no step.

---

### US-07 · Keyboard navigation

**As a** power user,  
**I want** to navigate the library entirely from the keyboard,  
**so that** I can browse without reaching for the mouse.

#### Acceptance criteria

- [ ] `⌘K` or `/` (when not in an input) opens the Command Palette.
- [ ] `?` (when not in an input) opens the Shortcuts overlay.
- [ ] `C` (when not in an input) toggles the Scenario Composer.
- [ ] `F` (when not in an input) toggles favourite on the currently selected step.
- [ ] `J` / `K` move selection down / up through the filtered list.
- [ ] `Escape` closes any open overlay (palette, shortcuts).
- [ ] None of these fire when the user is typing in a text input or contenteditable.

#### Implementation tasks

1. `KeyboardHandler.razor` — registers `window.addEventListener('keydown', ...)` via JSInterop on `OnAfterRenderAsync`.
2. Dispatches events to `IKeyboardBus` (simple in-process event bus with `Publish/Subscribe`).
3. Subscribers: `PaletteState`, `ComposerState`, `SelectionState`, `FavouritesStore`.
4. Unit test: given a synthetic key event, correct bus event is published; typing-context is suppressed.

---

### US-08 · Command Palette (⌘K)

**As a** developer,  
**I want** a ⌘K command palette with fuzzy search across all steps,  
**so that** I can jump to any step in under two keystrokes without touching the filter rail.

#### Acceptance criteria

- [ ] Opens as a modal overlay on `⌘K`, `/`, or the "Quick find" header button.
- [ ] Input auto-focuses when opened; `Escape` closes and returns focus.
- [ ] Default state (no query): top 50 steps sorted by usage desc.
- [ ] With a query: fuzzy match against pattern, type, domain, tags, param names; results sorted by fuzzy score desc.
- [ ] Each result row: type chip, highlighted pattern, domain label, usage count (`N×`).
- [ ] Arrow Up/Down navigate the list; highlighted row scrolls into view.
- [ ] Enter or click selects a step: closes palette, updates main selection and detail panel.
- [ ] Empty state: "No step matches `query`." + hint suggesting a file to create the step in.
- [ ] Results capped at 50.

#### Implementation tasks

1. `FuzzySearch` static class — `Score(string needle, string hay)` using substring/subsequence algorithm matching the mockup.
2. `Palette.razor` — modal overlay with input, meta line, scrollable result list.
3. JSInterop: `focusElement(id)` helper for auto-focus.
4. Unit tests: `FuzzySearch.Score` with exact match, word-start boost, subsequence, no match.

---

### US-09 · Scenario Composer

**As a** QA engineer,  
**I want** to compose a Gherkin scenario by adding steps from the library, then copy it as `.feature` text,  
**so that** I can draft scenarios without context-switching to my editor.

#### Acceptance criteria

- [ ] Composer docks to the bottom of the viewport; a tab bar shows the beaker icon, "Scenario Composer", step count, and a chevron toggle.
- [ ] `C` keyboard shortcut or clicking the tab toggles open/closed.
- [ ] Clicking `+` on a step row or "Add to scenario" in the detail panel adds the step and opens the composer.
- [ ] **Edit column**: scenario name input; ordered list of added steps.
- [ ] Each step row: drag handle, keyword (Given/When/Then or "And" when same type follows same type), step pattern, navigate-to button, remove button.
- [ ] Steps are reorderable by drag-and-drop; drag state applies `is-drag` CSS class.
- [ ] **Suggested next** strip: up to 4 chips derived from `suggestsNext` of the most-recently-added steps, deduped against already-added steps; clicking a chip adds it.
- [ ] **Output column**: live `.feature` preview (Feature + Scenario header, indented steps); "Copy .feature" button; "Clear" button (with confirmation dialog).
- [ ] Copy button is disabled when composer is empty.
- [ ] Scenario name defaults blank; when blank the preview reads `Feature: Untitled scenario`.
- [ ] The composed `.feature` text uses `And` keyword when consecutive steps have the same Gherkin type.

#### Implementation tasks

1. `ComposerState` service (scoped) — holds `List<ComposerItem>`, `string ScenarioName`; exposes Add, Remove, Reorder, Clear.
2. `ScenarioComposer.razor` — two-column layout, docking tab.
3. `ComposerRow.razor` — single step row with drag events wired via JSInterop (`ondragstart/over/end`).
4. `SuggestionStrip.razor` — computed from `suggestsNext` chains.
5. `FeaturePreview.razor` — computes `.feature` text; scrollable `<pre>`.
6. `DragDropService` — JS helper to pass drag index back to Blazor.
7. Unit tests: `FeatureTextBuilder` produces correct keyword assignment (Given/And transitions).

---

### US-10 · Keyboard shortcuts overlay

**As a** user,  
**I want** to see all keyboard shortcuts in an overlay by pressing `?`,  
**so that** I can learn the shortcuts without consulting documentation.

#### Acceptance criteria

- [ ] Pressing `?` (not in input) opens a centred modal overlay.
- [ ] Overlay lists all shortcuts with human-readable labels and `<kbd>` chips.
- [ ] Multiple bindings for the same action shown as `<kbd>A</kbd> or <kbd>B</kbd>`.
- [ ] Clicking outside the modal or pressing `Escape` closes it.
- [ ] Overlay is accessible: `role="dialog"`, focus-trapped while open.

#### Implementation tasks

1. `ShortcutsOverlay.razor` — data-driven list of `(label, keys[])` records.
2. Focus-trap via JSInterop on open.
3. bUnit test: renders correct number of rows; closes on Escape key event.

---

### US-11 · Appearance tweaks (dark mode, accent, density)

**As a** user,  
**I want** to toggle dark mode, choose an accent colour, and change list density,  
**so that** the viewer fits my working environment and reduces eye strain.

#### Acceptance criteria

- [ ] Dark mode toggle in the header switches the `data-dark` attribute on the root element.
- [ ] Tweaks panel (gear/settings icon, or triggered by the header) offers:
  - **Dark mode** toggle.
  - **Accent** radio: Teal / Amber / Violet (maps to `data-accent` on root).
  - **Density** radio: Comfortable / Compact (maps to `data-density`).
  - **Row emphasis** radio: Pattern / Meta.
  - **Source** radio: Collapsed / Expanded (default for the C# source block).
- [ ] All tweaks are persisted in `localStorage` under `docview.tweaks.v1` as a JSON object.
- [ ] Tweaks are restored on next load before first paint to avoid flash.

#### Implementation tasks

1. `TweaksStore` service — get/set/persist individual tweak keys.
2. `TweaksPanel.razor` — slide-out or overlay panel.
3. `RootAttributeWriter` — injects `data-*` attributes onto `<html>` via JSInterop.
4. CSS variables file: `--accent-*` mapped from `data-accent`; `--density-*` mapped from `data-density`.
5. Unit test: store round-trips through JSON serialisation.

---

### US-12 · Authentication — Entra ID (production) / bypass (development)

**As a** site operator,  
**I want** production deployments to require Entra ID login while development runs without authentication,  
**so that** the library is protected in production but developers can run it locally without AAD config.

#### Acceptance criteria

- [ ] When `ASPNETCORE_ENVIRONMENT=Development` (or env var `DOCVIEW_AUTH_DISABLED=true`), all pages are accessible without login; no redirect occurs.
- [ ] In non-Development environments, unauthenticated requests are redirected to the Entra ID login flow (OpenID Connect).
- [ ] After login, the user's display name is shown in the header avatar chip.
- [ ] In dev bypass mode the avatar shows `QA` (or initials derived from `DOCVIEW_DEV_USER` env var, default `QA`).
- [ ] Required OIDC config keys: `AzureAd:TenantId`, `AzureAd:ClientId`, `AzureAd:ClientSecret`, `AzureAd:CallbackPath`.
- [ ] Logout route `/logout` clears the session and redirects to `/`.

#### Implementation tasks

1. `Microsoft.Identity.Web` package added.
2. `AuthConfig` — reads environment; registers either `AddOpenIdConnect` or `AddNoOpAuth` (dev bypass middleware that injects a fake claims principal).
3. `DevBypassAuthHandler` — returns a synthetic `ClaimsPrincipal` from env var.
4. Header avatar reads `User.Identity.Name` (or fallback).
5. `appsettings.Production.json` template with `AzureAd` section.
6. `docker-compose.dev.yml` sets `ASPNETCORE_ENVIRONMENT=Development`.
7. Integration test: in dev mode, GET `/` returns 200 without auth cookie.

---

### US-13 · Docker deployment

**As a** DevOps engineer,  
**I want** to run the viewer as a Docker container with a mounted library file,  
**so that** updating the step library requires only replacing the mounted file and restarting the container, not rebuilding the image.

#### Acceptance criteria

- [ ] `Dockerfile` builds a release image from the official `mcr.microsoft.com/dotnet/aspnet:8.0` base.
- [ ] Library file is NOT baked into the image; it is expected at the path given by `DOCVIEW_LIBRARY_PATH` (default `/data/step-library.json`).
- [ ] Container listens on port 8080; `EXPOSE 8080` in Dockerfile.
- [ ] `docker-compose.yml` mounts `./data:/data` and sets required env vars.
- [ ] Health check endpoint `GET /health` returns `200 OK` with JSON `{"status":"healthy"}` when library is loaded, or `{"status":"unhealthy","reason":"..."}` with 503 when not.
- [ ] Multi-stage build: build stage uses SDK image; final stage uses runtime-only image.
- [ ] `.dockerignore` excludes `*.json` data files, `node_modules`, `.git`.

#### Implementation tasks

1. `Dockerfile` (multi-stage).
2. `docker-compose.yml` + `docker-compose.dev.yml`.
3. `.dockerignore`.
4. `HealthCheckController` (minimal API) at `/health`.
5. Smoke test: `docker build` + `docker run` + `curl /health` returns 200.

---

## Open Questions

| # | Question | Owner | Blocking? |
|---|----------|-------|-----------|
| 1 | Should the library file be hot-reloaded without restart when updated on disk? | Engineering | No |
| 2 | Is the SHA-256 signature check meant to be a hard failure or a warning? | Product | No — defaulting to warning |
| 3 | Are Entra ID group claims needed for role-based access, or is login-only sufficient for v1? | Stakeholder | No — login-only for v1 |
| 4 | Should the Scenario Composer export be saved server-side (audit trail), or clipboard-only? | Product | No — clipboard-only for v1 |

---

## Success Metrics

| Metric | Target | When |
|--------|--------|------|
| Time-to-find a known step (usability test) | < 10 s | 1 week post-deploy |
| Docker image size | < 250 MB | At release |
| Page load (first contentful paint, local) | < 1 s | At release |
| Zero schema-validation errors on prod library file | 100% | At release |

---

## Timeline Considerations

- No hard deadline. Implement stories in order: US-01 → US-02 → US-03 → US-04 → US-06 → US-08 → US-09 → US-05 → US-07 → US-10 → US-11 → US-12 → US-13.
- US-12 and US-13 can be parallelised after US-02 is complete.
- US-07 depends on US-03, US-04, US-06, and US-09 all being wired up.
