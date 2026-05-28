using Delta.DocView.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Delta.DocView.Client.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

builder.Services.AddScoped<ClientStepLibraryStore>();
builder.Services.AddScoped<LibraryApiClient>();
builder.Services.AddScoped<FilterState>();
builder.Services.AddScoped<SelectionState>();
builder.Services.AddScoped<IFavouritesStore, LocalStorageFavouritesStore>();
builder.Services.AddScoped<FilteredStepsProvider>();
builder.Services.AddScoped<IKeyboardActions, KeyboardActions>();
builder.Services.AddScoped<PaletteState>();
builder.Services.AddScoped<ComposerState>();
builder.Services.AddScoped<IPlatform, PlatformService>();

await builder.Build().RunAsync();
