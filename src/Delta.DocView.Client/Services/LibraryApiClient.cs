using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Delta.DocView.Shared;
using Microsoft.JSInterop;

namespace Delta.DocView.Client.Services;

public sealed class LibraryApiClient
{
    private readonly HttpClient _http;
    private readonly ClientStepLibraryStore _store;
    private readonly IJSRuntime _js;

    public LoadingState State { get; private set; } = LoadingState.Loading;
    public string? ErrorMessage { get; private set; }
    public string? WarningMessage { get; private set; }

    public LibraryApiClient(HttpClient http, ClientStepLibraryStore store, IJSRuntime js)
    {
        _http = http;
        _store = store;
        _js = js;
    }

    public async Task LoadAsync()
    {
        if (State != LoadingState.Loading) return;

        try
        {
            var response = await _http.GetAsync("/api/library");

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content
                    .ReadFromJsonAsync<JsonElement>();
                ErrorMessage = err.TryGetProperty("error", out var prop)
                    ? prop.GetString() ?? $"Server returned {(int)response.StatusCode}."
                    : $"Server returned {(int)response.StatusCode}.";
                State = LoadingState.Error;
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<LibraryResponse>();
            if (result is null)
            {
                ErrorMessage = "Server returned an empty response.";
                State = LoadingState.Error;
                return;
            }

            _store.Populate(result.Library);
            WarningMessage = result.Warning;
            State = LoadingState.Loaded;

            try
            {
                await _js.InvokeVoidAsync("docview.setDomainPalette", BuildPaletteCss());
            }
            catch
            {
                // CSS injection is non-critical
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load step library: {ex.Message}";
            State = LoadingState.Error;
        }
    }

    private string BuildPaletteCss()
    {
        var sb = new StringBuilder();
        sb.Append(":root {");
        foreach (var domain in _store.Domains)
        {
            sb.Append(' ');
            sb.Append(DomainPalette.CssVarName(domain.Id));
            sb.Append(": ");
            sb.Append(DomainPalette.CssVarValue(domain.Id));
            sb.Append(';');
        }
        sb.Append(" }");
        return sb.ToString();
    }
}
