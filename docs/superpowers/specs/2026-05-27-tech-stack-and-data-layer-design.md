# Tech Stack & Data Layer Design

**Date:** 2026-05-27  
**Status:** Approved  
**Scope:** Overall solution architecture, project structure, server/client data flow, and service boundaries for Delta.DocView.

---

## Problem

The mockup for Delta.DocView is highly interactive — real-time text filtering, keyboard shortcuts, drag-and-drop in the composer. A pure Blazor Server approach puts every keystroke on a SignalR round-trip, which is unacceptable for users on a corporate WAN or VPN. The solution must work well regardless of network latency.

---

## Decisions

| Question | Decision | Rationale |
|----------|----------|-----------|
| Rendering model | Blazor WebAssembly (hosted) | All UI interactions run in the browser — zero round-trips after initial load |
| Data storage | Singleton in-memory store | Read-only reference data, ~2k items; no SQLite/EF Core needed |
| Shared models | Internal `Shared` project (Server + Client only) | Delta.DocGen is a separate repo; the JSON schema is the contract between them |
| Auth | Entra ID OIDC in production; bypass in development | `ASPNETCORE_ENVIRONMENT=Development` disables auth entirely |

---

## Solution Structure

```
Delta.DocView.sln
src/
  Delta.DocView.Shared/          # domain models only — no UI, no services
    Models/
      StepLibrary.cs
      Step.cs
      StepParam.cs
      StepDomain.cs
      StepSignature.cs
    LibraryResponse.cs           # shared API DTO (Server returns, Client deserialises)
    Delta.DocView.Shared.csproj

  Delta.DocView.Server/          # ASP.NET Core host
    Program.cs
    Services/
      IStepLibraryStore.cs
      StepLibraryStore.cs        # singleton; populated once at startup
      StepLibraryLoader.cs       # file I/O + JSON deserialise
      StepLibraryValidator.cs    # JsonSchema.Net validation against embedded schema
      SignatureVerifier.cs       # SHA-256 digest check
      IStartupError.cs
      StartupError.cs            # singleton; records error or warning from startup
      StartupLoader.cs           # orchestrates Load → Validate → Verify
    Controllers/
      LibraryController.cs       # GET /api/library  (authenticated)
      HealthController.cs        # GET /health       (anonymous)
    Schemas/
      step-library.v1.schema.json  # embedded resource
    Delta.DocView.Server.csproj

  Delta.DocView.Client/          # Blazor WASM
    Program.cs
    Services/
      LibraryApiClient.cs        # fetches /api/library once on startup
      ClientStepLibraryStore.cs  # singleton; holds full library in memory
      FilterEngine.cs            # pure static: Apply(steps, FilterState, query)
      FuzzySearch.cs             # pure static: Score(needle, hay)
      FavouritesStore.cs         # localStorage via JSInterop
      ComposerState.cs           # WIP scenario; fires StateChanged
      SelectionState.cs          # selected step ID; fires StateChanged
    Components/
      App.razor                  # root; branches on LoadingState / StartupError
      LoadingScreen.razor        # shown during WASM boot and library fetch
      StartupErrorPage.razor     # shown when server returns 503
      Layout/
        MainLayout.razor
        Header.razor
        LeftRail.razor
        ...                      # US-02 through US-11 components
    wwwroot/
      index.html                 # static loading screen (pre-WASM-boot)
    Delta.DocView.Client.csproj

tests/
  Delta.DocView.Tests/
    Services/                    # xUnit — server and client services
    Components/                  # bUnit — Blazor components
    TestData/                    # JSON fixtures
    Delta.DocView.Tests.csproj
```

**Project references:**
- `Shared` → nothing
- `Server` → `Shared`
- `Client` → `Shared`
- `Tests` → `Server`, `Client`, `Shared`

---

## Data Flow

### Server startup (once, on container start)

```
Program.cs resolves DOCVIEW_LIBRARY_PATH env var
  (default: /data/step-library.json in production,
            data/step-library.json relative to ContentRoot in development)
  │
  ├─ StepLibraryLoader.Load(path)
  │     • FileNotFoundException  → StartupError.SetError(...)
  │     • JsonException          → StartupError.SetError(...)
  │
  ├─ StepLibraryValidator.Validate(rawJson)
  │     • Invalid schema         → StartupError.SetError(errors joined, max 5)
  │
  ├─ SignatureVerifier.Verify(rawJson, digest)
  │     • Mismatch               → StartupError.SetWarning(...)   ← loads anyway
  │
  └─ StepLibraryStore.Populate(library)
        • IsLoaded = true
        • App always starts — error is surfaced via API, not a crash
```

### Client first load (once per browser session)

```
Browser loads index.html → static loading screen visible immediately
  │
  └─ Blazor WASM runtime downloads and boots
        │
        └─ App.razor renders → LoadingState = Loading → LoadingScreen shown
              │
              └─ LibraryApiClient.LoadAsync()
                    │
                    ├─ GET /api/library → 503  → LoadingState = Error
                    │                           → StartupErrorPage shown
                    │
                    └─ GET /api/library → 200  → ClientStepLibraryStore.Populate(library)
                                                → LoadingState = Loaded
                                                → MainLayout shown
                                                → (warning banner if response.Warning != null)

After this point: ALL filtering, search, and composer interactions are
pure in-memory WASM — zero further server calls during normal usage.
```

---

## API Contract

### `GET /api/library`

| | |
|---|---|
| **Auth** | `[Authorize]` in Production; anonymous in Development |
| **200 OK** | `LibraryResponse` |
| **503 Service Unavailable** | `{ "error": "..." }` |

```csharp
// Delta.DocView.Shared/LibraryResponse.cs
public record LibraryResponse(StepLibrary Library, string? Warning);
```

### `GET /health`

| | |
|---|---|
| **Auth** | Always anonymous (Docker health check) |
| **200 OK** | `{ "status": "healthy" }` |
| **503 Service Unavailable** | `{ "status": "unhealthy", "reason": "..." }` |

---

## Server-Side Services

| Service | Lifetime | Responsibility |
|---------|----------|----------------|
| `StepLibraryLoader` | Transient (startup only) | Read file, deserialise JSON |
| `StepLibraryValidator` | Transient (startup only) | Validate against embedded schema |
| `SignatureVerifier` | Static | SHA-256 of JSON-without-signature |
| `StartupLoader` | Static | Orchestrate load → validate → verify → populate |
| `StartupError` | Singleton | Record startup error or warning; read by controller |
| `StepLibraryStore` | Singleton | Hold loaded library; `IsLoaded` flag |

`StepLibraryLoader`, `StepLibraryValidator`, and `StartupLoader` are called directly in `Program.cs` and are not registered in the DI container as long-lived services.

---

## Client-Side Services

All registered as singletons in WASM `Program.cs` unless noted.

| Service | Responsibility |
|---------|----------------|
| `LibraryApiClient` | Single `GET /api/library` call; exposes `LoadingState` enum (Loading / Loaded / Error) |
| `ClientStepLibraryStore` | Holds `IReadOnlyList<Step>`, `IReadOnlyList<StepDomain>`, `Dictionary<string,Step> ById` |
| `FilterEngine` | `static Apply(IReadOnlyList<Step>, FilterState, string query) → IReadOnlyList<Step>` |
| `FuzzySearch` | `static int Score(string needle, string hay)` — substring with word-start boost, then subsequence fallback |
| `FavouritesStore` | `Toggle(id)`, `Has(id)`, `int Count`; persists to `localStorage` key `docview.favs.v1` |
| `ComposerState` | `List<ComposerItem>`, `string ScenarioName`; Add / Remove / Reorder / Clear; fires `StateChanged` |
| `SelectionState` | `string? SelectedStepId`; fires `StateChanged`; central selection source of truth |

```csharp
// FilterState — plain record, no dependencies
public record FilterState(
    IReadOnlySet<string> ActiveTypes,     // {"Given","When","Then"} — never empty
    string? ActiveDomain,                 // null = All
    IReadOnlySet<string> ActiveParamTypes,// empty = no filter
    bool FavsOnly
);
```

---

## Loading Screen

Two sequential phases, visually seamless:

1. **Pre-WASM** — static HTML/CSS in `wwwroot/index.html` inside the `<div id="app">` placeholder. Visible from the first byte of the page load, before any JavaScript runs. Shows the Delta logo, app name, and a spinner.

2. **Post-WASM** — once Blazor starts, `App.razor` checks `LibraryApiClient.LoadingState`. While `Loading`, it renders `<LoadingScreen />` (a Blazor component with the same visual). The handoff is invisible to the user.

---

## Auth

| Environment | Behaviour |
|-------------|-----------|
| `Development` | No auth. `LibraryController` is accessible without credentials. Header avatar shows initials from `DOCVIEW_DEV_USER` env var (default: `QA`). |
| Production | `[Authorize]` on `LibraryController`. OIDC redirect via `Microsoft.Identity.Web`. After login, display name shown in header avatar. |

`HealthController` is always anonymous — Docker's health check runs without credentials.

Required env vars in production: `AzureAd__TenantId`, `AzureAd__ClientId`, `AzureAd__ClientSecret`.

---

## Testing

| Layer | Framework | Notes |
|-------|-----------|-------|
| Server services (Loader, Validator, Verifier, StartupLoader) | xUnit | File I/O uses temp files or `TestData/` fixtures |
| Client services (FilterEngine, FuzzySearch, ComposerState, SelectionState) | xUnit | Pure in-memory, no infrastructure |
| `FavouritesStore` | bUnit | Mocked `IJSRuntime` |
| Blazor components (LoadingScreen, StartupErrorPage, and all UI) | bUnit | Per-component, `NSubstitute` for service dependencies |
| HTTP endpoints | Not in scope for v1 | Deferred to future E2E story |

---

## Out of Scope (v1)

- Server-side filtering or pagination of steps
- Persisting favourites or composer state server-side
- Multiple library files per instance
- Hot-reload of the library file without container restart
- Sharing models with Delta.DocGen via a NuGet package
