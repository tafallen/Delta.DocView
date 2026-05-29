using Delta.DocView.Server.Auth;
using Delta.DocView.Server.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

// Startup error and library store — singletons read by controllers
builder.Services.AddSingleton<StartupError>();
builder.Services.AddSingleton<IStartupError>(sp => sp.GetRequiredService<StartupError>());
builder.Services.AddSingleton<StepLibraryStore>();
builder.Services.AddSingleton<IStepLibraryStore>(sp => sp.GetRequiredService<StepLibraryStore>());

// Loader/validator are stateless — register so startup resolves them from DI
builder.Services.AddSingleton<StepLibraryLoader>();
builder.Services.AddSingleton<StepLibraryValidator>();

builder.AddDocViewAuth();

var app = builder.Build();

// ── Load the step library at startup ────────────────────────────────────────
var libraryPath = app.Configuration["DOCVIEW_LIBRARY_PATH"]
    ?? Path.Combine(app.Environment.ContentRootPath, "data", "step-library.json");

// Resolve relative paths against the content root so config can name files
// relative to the server project regardless of the launch working directory.
if (!Path.IsPathRooted(libraryPath))
    libraryPath = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, libraryPath));

var startupError = app.Services.GetRequiredService<StartupError>();
var startupStore = app.Services.GetRequiredService<StepLibraryStore>();
var loader       = app.Services.GetRequiredService<StepLibraryLoader>();
var validator    = app.Services.GetRequiredService<StepLibraryValidator>();

StartupLoader.Run(libraryPath, loader, validator, startupError, startupStore);

if (startupError.HasError)
    app.Logger.LogError("Startup error: {Error}", startupError.ErrorMessage);
else if (startupError.HasWarning)
    app.Logger.LogWarning("Startup warning: {Warning}", startupError.WarningMessage);
else
    app.Logger.LogInformation(
        "Step library loaded: {Count} steps, version {Version}, generated {GeneratedAt}",
        startupStore.Library!.Steps.Count,
        startupStore.Library.Version,
        startupStore.Library.GeneratedAt);

// ── Middleware ───────────────────────────────────────────────────────────────
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program { }
