using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Lumi.Api.Tests;

public static class ApiHelpers
{
    public sealed record Auth(string PlayerId, string Username, string DisplayName, string AccessToken, string ExpiresAt);

    public static string NewName() => "p_" + Guid.NewGuid().ToString("N")[..10];

    public static async Task<Auth> RegisterAsync(this HttpClient client, string? username = null)
    {
        username ??= NewName();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { username, password = "password123", confirmPassword = "password123" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Auth>())!;
    }

    public static HttpClient As(this LumiApiFactory factory, Auth auth)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response)
    {
        string text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    public static async Task<string?> CodeAsync(this HttpResponseMessage response)
    {
        var json = await response.JsonAsync();
        return json.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}
