using System.Net.Http.Json;
using System.Text.Json;

namespace Delta.DocView.Client.Services;

/// <summary>
/// Fetches the current user's display info from the server's /api/user
/// endpoint at boot. Returns UserInfo.Fallback on any failure.
/// </summary>
public sealed class UserClient
{
    private readonly HttpClient _http;

    public UserInfo Current { get; private set; } = UserInfo.Fallback;

    public UserClient(HttpClient http) { _http = http; }

    public async Task LoadAsync()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<UserDto>("/api/user",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result is not null)
                Current = new UserInfo(result.Name ?? "QA",
                                       result.Initials ?? "QA",
                                       result.Authenticated);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UserClient.LoadAsync failed: {ex.Message}");
        }
    }

    private sealed record UserDto(string? Name, string? Initials, bool Authenticated);
}
