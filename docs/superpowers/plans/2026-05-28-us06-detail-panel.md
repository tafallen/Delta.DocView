# Delta.DocView — US-06 Detail Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans`. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Fill the right-hand `DetailPanel` with the full read-out of the currently selected step — header chip + favourite, large pattern with typed parameter pills, description, stats strip, parameters table, an interactive "Try it" composer with copy-to-clipboard, a collapsible syntax-highlighted C# source block, and a "Likely next steps" carousel of related steps. Driven entirely by `SelectionState` — the writer (`StepRow`) already exists.

**Architecture:**

- `DetailPanel.razor` injects `SelectionState`, subscribes to `Changed`, and either renders nothing (when `Selected is null`) or composes the six display components below. No `EventCallback` plumbing — selection state is the contract.
- A new static utility `PatternTokeniser.Tokenise(pattern)` extracts the token-parsing logic currently inline in `PatternRenderer.razor`. `PatternRenderer` and `TryItSection` both consume it, so token grammar lives in one place.
- "Try it" composes a live `.feature`-style line by walking the tokens and substituting param values from a `Dictionary<int, string>` keyed by token index (positional binding — handles unnamed `{type}` tokens too).
- Copy buttons use a JS helper `docview.copyText(text)` (a thin wrapper over `navigator.clipboard.writeText`). The "✓ line copied" confirmation is C# component-local state with a `System.Threading.Timer` cleared after 1.1 s.
- C# syntax highlighting is a small in-process token pass over a fixed keyword set + strings + comments + numbers. Not Roslyn — a regex-driven tokeniser is enough to colour the source block at v1 fidelity.
- "Likely next steps" reads `step.SuggestsNext` (list of step ids), resolves each via `ClientStepLibraryStore.ById`, displays up to four as clickable cards that call `Selection.Select(otherStep)`.

**Tech stack additions:** none.

---

## File Map

```
src/Delta.DocView.Client/
  Services/
    PatternTokeniser.cs           ← new (lifted out of PatternRenderer)
    PatternToken.cs               ← new (record: static text OR param token)
    CSharpHighlighter.cs          ← new (regex-driven keyword/string/comment tokeniser)
  Components/
    PatternRenderer.razor         ← UPDATED to use PatternTokeniser
    PatternHeading.razor          ← new (large pattern wrapper, no highlight)
    StatsStrip.razor              ← new (used-count / source / tags)
    ParamsTable.razor             ← new (name / type / example rows)
    TryItSection.razor            ← new (input-per-param + live preview + copy)
    CSharpBlock.razor             ← new (collapsible highlighted C# + copy)
    RelatedSteps.razor            ← new (up to 4 clickable cards from SuggestsNext)
    DetailPanel.razor             ← UPDATED — composes all of the above
  wwwroot/js/docview.js           ← UPDATED — adds copyText helper

src/Delta.DocView.Client/wwwroot/css/app.css   ← appended detail-panel styles

tests/Delta.DocView.Tests/
  Services/
    PatternTokeniserTests.cs
    CSharpHighlighterTests.cs
  Components/
    PatternHeadingTests.cs
    StatsStripTests.cs
    ParamsTableTests.cs
    TryItSectionTests.cs
    CSharpBlockTests.cs
    RelatedStepsTests.cs
    DetailPanelTests.cs
  Integration/
    FilterStackTests.cs          ← EXTENDED — selection → detail panel renders correctly
```

---

## Design notes

- **`PatternToken`**: a record-or-sealed-class with two shapes — `StaticText(string text)` and `ParamToken(string? name, string type)`. The tokeniser emits an `IReadOnlyList<PatternToken>` in document order. Static text always present where there isn't a token (empty static segments are allowed if two tokens abut, but more naturally collapsed). Empty/malformed `{}` and `{name:}` shapes fall back to `StaticText` containing the literal `{...}` — matching the behaviour TD-D pinned for PatternRenderer.
- **Try-it composition**: walk the tokens; for each `ParamToken` at index `i` look up `_values[i] ?? param.Example ?? "?"`. For static text, append verbatim. Join into a single line. Display below the inputs.
- **Inputs labelled** by param name (falling back to type if name absent). One `<input>` per param, side-by-side or stacked. Bound via `@bind-Value:event="oninput"` so the preview updates live.
- **Copy button states**: idle / confirmed. The `Copy` method sets `_confirmed = true; _timer.Change(1100, Timeout.Infinite);`. On tick, sets `_confirmed = false; InvokeAsync(StateHasChanged);`. Dispose timer on component disposal.
- **C# highlighter token kinds**: `keyword`, `string`, `comment`, `number`, `default`. Keyword list lives in `CSharpHighlighter` as a `HashSet<string>` of the common ones (`public`, `void`, `class`, `var`, `string`, `int`, `bool`, `if`, `else`, `return`, `new`, `using`, `namespace`, `async`, `await`, `Task`, `static`, `readonly`, `private`, etc — about 30). Strings: `"…"` with `\"` escapes; verbatim `@"…"` and interpolated `$"…"` recognised but treated as a single string region. Comments: `//` to EOL and `/* … */`. Numbers: `\b\d+\b` for integer/long literals. Identifier defaults pass through unstyled.
- **`CSharpBlock` default state**: collapsed (US-11 tweaks will toggle the default). Toggle arrow chevron flips on click. When expanded, "Copy source" appears in the block header.
- **Related steps**: up to four cards. Each shows the related step's type chip + truncated pattern (first 60 chars). Click → `Selection.Select(otherStep)`. If `SuggestsNext` is empty or no ids resolve, the whole section is hidden.
- **Empty-selection state**: `<p class="detail-empty">Select a step to see details.</p>` — friendly, three lines of markup. Not a full splash screen.
- **"Add to scenario" primary button** in the detail header: same disabled stub as the row `+` button, using `UiStrings.AddToScenarioComingSoon`. US-09 wires both call sites at once.
- **Favourite button** in the detail header: same `IFavouritesStore.Toggle(step.Id)` call as `StepRow.OnStarClick`. Star icon + `is-fav` class mirrors the row. Subscribes to `Favs.Changed` so external toggles (row star) reflect here.

---

## Tasks

### Task 1 — Extract `PatternTokeniser` + refactor `PatternRenderer`

- [ ] Create `src/Delta.DocView.Client/Services/PatternToken.cs`: an `abstract record PatternToken;` with `sealed record StaticText(string Text) : PatternToken;` and `sealed record ParamToken(string? Name, string Type) : PatternToken;`.
- [ ] Create `src/Delta.DocView.Client/Services/PatternTokeniser.cs`: `public static IReadOnlyList<PatternToken> Tokenise(string pattern)`. Use the existing regex `\{([^}]*)\}`. For each match:
    - Empty inner → emit `StaticText("{}")`.
    - Split on first `:` → name + type, trim each.
    - If type empty → emit `StaticText(match.Value)`.
    - If name empty → `ParamToken(null, type)`.
    - Otherwise → `ParamToken(name, type)`.
    Static segments between/around matches → `StaticText(...)`. Skip emitting empty static segments.
- [ ] Refactor `src/Delta.DocView.Client/Components/PatternRenderer.razor` to consume `PatternTokeniser.Tokenise(Pattern)` instead of running the regex inline. Behaviour unchanged. All existing `PatternRendererTests` must still pass.
- [ ] Tests in `tests/Delta.DocView.Tests/Services/PatternTokeniserTests.cs`:
    - Plain text → single `StaticText` token.
    - `{string}` → one `ParamToken(null, "string")`.
    - `{name : string}` → one `ParamToken("name", "string")`.
    - `{}` → one `StaticText("{}")`.
    - `{:type}` → one `ParamToken(null, "type")`.
    - `{name:}` → one `StaticText("{name:}")`.
    - `{a:b:c}` → one `ParamToken("a", "b:c")`.
    - Multiple tokens interleaved → correct order and types.

### Task 2 — `PatternHeading` + `StatsStrip` + `ParamsTable`

Three small read-only display components, bundled into one commit (they share zero state and have nothing interesting beyond markup).

- [ ] `PatternHeading.razor`: parameter `Step Step`. Renders `<h2 class="detail-pattern"><PatternRenderer Pattern="@Step.Pattern" /></h2>` (no query — no highlighting in detail view).
- [ ] `StatsStrip.razor`: parameter `Step Step`. Renders three cells: "Used in N scenarios", "Source `File:Line`", and "Tags `t1 · t2 · t3`" (joined with ` · `; section hidden when no tags).
- [ ] `ParamsTable.razor`: parameter `Step Step`. Hidden entirely when `Step.Params.Count == 0`. Otherwise renders a `<table class="params-table">` with header row Name / Type / Example and one row per param. Type cell shows a coloured chip via the existing `param-{type}` CSS classes from US-04. Example cell renders verbatim text (no rendering quotes — examples are stored as `"\"value\""` already, just print).
- [ ] Tests for each (one bUnit file per component):
    - `PatternHeadingTests`: renders an `<h2>` containing a `PatternRenderer` output (assert `.detail-pattern` and one `.param-pill` for a typed step).
    - `StatsStripTests`: shows `Used in 7 scenarios` for `Step.Used == 7`; shows file path and line; tag row shown for >0 tags; tag row HIDDEN when tags empty.
    - `ParamsTableTests`: hidden entirely when no params; renders one row per param with correct columns; type cell has `param-string` class for a string param.

### Task 3 — JS clipboard helper + `docview.copyText`

- [ ] Append to `src/Delta.DocView.Client/wwwroot/js/docview.js`:
    ```js
    copyText: async function (text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch (e) {
            console.warn('docview.copyText failed', e);
            return false;
        }
    }
    ```
- [ ] No new tests for the JS itself. The .NET consumers will mock `IJSRuntime`.

### Task 4 — `TryItSection`

- [ ] Create `src/Delta.DocView.Client/Components/TryItSection.razor`. Parameter `Step Step`. Inject `IJSRuntime`. `@implements IDisposable`.
- [ ] Tokenise `Step.Pattern` via `PatternTokeniser.Tokenise`. Walk tokens; for each `ParamToken` at index `i`, render a labelled `<input>` bound to `_values[i]`. Initial value: matching `Step.Params[i]` `.Example` if present (positional binding — `params` and `ParamToken`s are assumed in same order).
- [ ] Below the inputs, render the live composed line: walk tokens again, emit static text verbatim and substitute param input values into `ParamToken` positions. Compose into a single string; display inside `<pre class="composed-line">`.
- [ ] `Copy` button calls `await JS.InvokeAsync<bool>("docview.copyText", composedLine);` then sets `_confirmed = true`; a `System.Threading.Timer` clears `_confirmed` after 1100ms via `InvokeAsync(StateHasChanged)`. Dispose the timer.
- [ ] If `Step.Params.Count == 0`, skip inputs entirely and just render the static pattern as the composed line + Copy button.
- [ ] Tests in `tests/Delta.DocView.Tests/Components/TryItSectionTests.cs`:
    - Renders one input per param, prefilled with `Example` values.
    - Typing into the first input updates the composed line preview.
    - Click Copy → `IJSRuntime.InvokeAsync<bool>("docview.copyText", ...)` invoked with the composed line.
    - After Copy, the confirmation text `"✓ line copied"` is rendered for ≤1.1 s. Use bUnit's `WaitForAssertion` to verify it disappears.
    - No-params step: no inputs rendered; composed line equals `Step.Pattern`; Copy button still works.

### Task 5 — `CSharpHighlighter` + `CSharpBlock`

- [ ] Create `src/Delta.DocView.Client/Services/CSharpHighlighter.cs`. Static class with `IReadOnlyList<HighlightedToken> Tokenise(string source)`. `HighlightedToken` is `(string Text, string CssClass)`. Walk the source via a small tokeniser:
    - `//` to end of line → class `cs-comment`.
    - `/* … */` (multi-line) → class `cs-comment`.
    - `"…"` (with `\"` escapes), `@"…"`, `$"…"` → class `cs-string`.
    - `\b\d+\b` → class `cs-number`.
    - Identifier matching keyword list → class `cs-kw`.
    - Everything else → class `cs-text`.
    Keyword set: roughly 30 common C# keywords (`public`, `private`, `protected`, `internal`, `static`, `readonly`, `var`, `void`, `class`, `record`, `struct`, `interface`, `enum`, `new`, `using`, `namespace`, `if`, `else`, `for`, `foreach`, `while`, `return`, `break`, `continue`, `string`, `int`, `bool`, `double`, `decimal`, `Task`, `async`, `await`, `null`, `true`, `false`).
- [ ] Tests in `tests/Delta.DocView.Tests/Services/CSharpHighlighterTests.cs`:
    - Keywords get `cs-kw`.
    - Strings get `cs-string`.
    - Line comments get `cs-comment`.
    - Block comments get `cs-comment` and span multiple lines.
    - Numbers get `cs-number`.
    - Identifiers not matching the keyword list get `cs-text`.
    - Reassembled tokens equal the original source (no characters lost).
- [ ] Create `src/Delta.DocView.Client/Components/CSharpBlock.razor`. Parameter `string Source`. Local state `bool _expanded`.
    - Collapsed: shows `<button class="cs-toggle">▸ Show C# step definition</button>`.
    - Expanded: shows `<button class="cs-toggle">▾ Hide C# step definition</button>`, plus `<pre class="cs-source">` containing `@foreach (var token in CSharpHighlighter.Tokenise(Source)) { <span class="@token.CssClass">@token.Text</span> }`, plus a "Copy source" button using the same JS-interop pattern + 1.1 s confirmation as TryItSection.
- [ ] Tests in `tests/Delta.DocView.Tests/Components/CSharpBlockTests.cs`:
    - Collapsed by default — no `.cs-source` element present.
    - Clicking toggle expands; `.cs-source` appears.
    - Clicking again collapses.
    - When expanded, "Copy source" button is present.
    - Copy click invokes `IJSRuntime.InvokeAsync<bool>("docview.copyText", source)`.

### Task 6 — `RelatedSteps`

- [ ] Create `src/Delta.DocView.Client/Components/RelatedSteps.razor`. Parameter `Step Step`. Inject `ClientStepLibraryStore Store`, `SelectionState Selection`.
- [ ] Resolve `Step.SuggestsNext` ids via `Store.ById`, drop ids not found, take up to 4. Render nothing (no header, no container) when the resolved list is empty.
- [ ] Each card: `<button class="related-card" data-step-id="@id">{type-chip}{truncated-pattern}</button>`. Truncate pattern to ~60 chars with ellipsis. Click → `Selection.Select(relatedStep)`.
- [ ] Tests in `tests/Delta.DocView.Tests/Components/RelatedStepsTests.cs`:
    - Empty `SuggestsNext` → component renders nothing (markup empty).
    - 6 `SuggestsNext` entries, 5 resolvable → renders exactly 4 cards.
    - Card click → `Selection.Selected.Id` becomes the clicked id.
    - Unresolvable ids (not in `Store.ById`) are silently skipped.

### Task 7 — `DetailPanel`

- [ ] Replace `src/Delta.DocView.Client/Components/DetailPanel.razor`. Inject `SelectionState Selection`, `IFavouritesStore Favs`. `@implements IDisposable`.
- [ ] Subscribe to `Selection.Changed` and `Favs.Changed` in `OnInitialized`. Unsubscribe in `Dispose`. Handler `InvokeAsync(StateHasChanged)`.
- [ ] When `Selection.Selected is null`: render `<div class="detail-empty" data-testid="detail-empty"><p>Select a step to see details.</p></div>`.
- [ ] Otherwise compose:
    ```razor
    <div class="detail-panel" data-step-id="@step.Id">
        <div class="detail-header">
            <span class="type-chip chip-@step.Type.ToLowerInvariant()">@step.Type</span>
            <span class="domain-dot" style="background:var(@(DomainPalette.CssVarName(step.Domain)));"></span>
            <span class="domain-label">@DomainLabel(step.Domain)</span>
            <button class="detail-favourite @(Favs.Has(step.Id) ? "is-fav" : "")" @onclick="OnFavClick">★</button>
            <button class="detail-add-primary" disabled title="@UiStrings.AddToScenarioComingSoon">Add to scenario</button>
        </div>
        <PatternHeading Step="@step" />
        <p class="detail-description">@step.Description</p>
        <StatsStrip Step="@step" />
        <ParamsTable Step="@step" />
        <TryItSection Step="@step" />
        <CSharpBlock Source="@step.Source" />
        <RelatedSteps Step="@step" />
    </div>
    ```
- [ ] Domain label resolution: small private helper that looks up `step.Domain` in `Store.Domains` (inject store too), falls back to the id when not found.
- [ ] Tests in `tests/Delta.DocView.Tests/Components/DetailPanelTests.cs`:
    - No selection → `data-testid="detail-empty"` rendered; no `.detail-panel` element.
    - With selection → `.detail-panel` present, includes type chip with `chip-given` for a Given step.
    - Favourite button toggles `IFavouritesStore.Has(step.Id)`.
    - Favourite button reflects external `Favs.Toggle(step.Id)` via subscription.
    - Add-to-scenario button is rendered with `disabled`.
    - Selection change (`Selection.Select(otherStep)`) re-renders the panel with the new step (assert `data-step-id` changes).

### Task 8 — Extend integration tests + CSS

- [ ] Append CSS rules to `src/Delta.DocView.Client/wwwroot/css/app.css` for: `.detail-panel`, `.detail-empty`, `.detail-header`, `.detail-pattern`, `.detail-description`, `.params-table`, `.try-it`, `.composed-line`, `.copy-confirmation`, `.cs-toggle`, `.cs-source`, `.cs-kw`, `.cs-string`, `.cs-comment`, `.cs-number`, `.cs-text`, `.related-card`, `.detail-favourite`, `.detail-add-primary`. Keep additions terse — functional styling, no theming yet. Use the existing CSS custom-property tokens where applicable (`--chip-*`, `--fav-color`, `--highlight-bg`, etc.).
- [ ] Extend `tests/Delta.DocView.Tests/Integration/FilterStackTests.cs` with new tests that render `Header + LeftRail + StepList + DetailPanel` in one `TestContext`:
    1. **Detail empty by default**: with no row clicked, detail panel shows `[data-testid="detail-empty"]`.
    2. **Selection populates detail**: click a row; detail panel renders `.detail-panel` with the same `data-step-id`.
    3. **Favourite toggle from row reflects in detail**: click the row's star; the detail panel's favourite button gains `is-fav` (verifies cross-component subscription).
    4. **Related card click changes selection**: select step A; click a `.related-card` in the detail; assert `SelectionState.Selected.Id` updates and the detail panel re-renders with the new step id.

---

## Out of scope (deferred)

- **`+` "Add to scenario" wiring**: US-09 wires both this button and the row's `+` button at once.
- **Configurable C# block default state**: US-11 (appearance tweaks) will expose a "Source: collapsed / expanded" preference. The component reads from `TweaksStore` then; for now hardcoded `_expanded = false`.
- **Real syntax highlighting**: the simple regex tokeniser will mis-highlight some constructs (interpolated string holes, nested generics, etc.). Good enough for v1; revisit if user complaints arrive.
- **Pattern-render highlighting in detail view**: `PatternHeading` passes no `Query` parameter — the detail panel is a focused read-out, not a search result. Confirmed by the spec wording (no mention of highlight in US-06 AC).

---

## Risk + open questions

| # | Question | Owner | Decision |
|---|----------|-------|----------|
| 1 | Positional vs name-based param matching in TryItSection — what if `Step.Params` and pattern tokens drift? | Engineering | Positional; if the library producer ever emits mis-ordered params/tokens, that's a data-quality bug worth surfacing — TryItSection should not paper over it. |
| 2 | C# highlighter — keyword set fidelity? | Engineering | The ~30-keyword set is the v1 floor. Edge cases (e.g. `record class`, `init`, `nint`) silently fall back to `cs-text`. Acceptable. |
| 3 | Copy confirmation timer — what if the user clicks Copy twice rapidly? | Engineering | Each click resets the timer's due time. Effectively the confirmation stays visible 1.1 s after the LAST click. |
| 4 | Should the detail panel auto-scroll into view when selection changes from the keyboard (US-07) or palette (US-08)? | UX | Defer — scroll-on-select is a US-07/08 concern, not US-06's. |
| 5 | Should the detail panel show the step's `Tags` separately, or only inside `StatsStrip`? | Product | Only inside `StatsStrip` per spec. |
