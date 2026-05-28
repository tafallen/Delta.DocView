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

await builder.Build().RunAsync();
