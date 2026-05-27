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

builder.Services.AddSingleton<ClientStepLibraryStore>();
builder.Services.AddSingleton<LibraryApiClient>(sp =>
    new LibraryApiClient(
        sp.GetRequiredService<HttpClient>(),
        sp.GetRequiredService<ClientStepLibraryStore>()));

await builder.Build().RunAsync();
