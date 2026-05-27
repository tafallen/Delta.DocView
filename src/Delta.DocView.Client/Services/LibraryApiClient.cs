using System.Net.Http.Json;
using System.Text.Json;
using Delta.DocView.Shared;

namespace Delta.DocView.Client.Services;

public sealed class LibraryApiClient
{
    private readonly HttpClient _http;
    private readonly ClientStepLibraryStore _store;

    public LoadingState State { get; private set; } = LoadingState.Loading;
    public string? ErrorMessage { get; private set; }
    public string? WarningMessage { get; private set; }

    public LibraryApiClient(HttpClient http, ClientStepLibraryStore store)
    {
        _http = http;
        _store = store;
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
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load step library: {ex.Message}";
            State = LoadingState.Error;
        }
    }
}
