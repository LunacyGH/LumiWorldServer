using System.Net;
using System.Net.Http.Json;

namespace Lumi.Api.Tests;

[Collection("api")]
public sealed class AuthTests(LumiApiFactory factory)
{
    [Fact]
    public async Task Register_then_login_returns_same_player()
    {
        var client = factory.CreateClient();
        string name = ApiHelpers.NewName();

        var registered = await client.RegisterAsync(name);
        Assert.False(string.IsNullOrEmpty(registered.AccessToken));
        Assert.Equal(name, registered.DisplayName);
        Assert.True(DateTimeOffset.TryParse(registered.ExpiresAt, out _));

        // Tên không phân biệt hoa thường khi đăng nhập
        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { username = name.ToUpperInvariant(), password = "password123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.Content.ReadFromJsonAsync<ApiHelpers.Auth>();
        Assert.Equal(registered.PlayerId, auth!.PlayerId);

        var me = await factory.As(auth).GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(name, (await me.JsonAsync()).GetProperty("username").GetString());
    }

    [Fact]
    public async Task Register_same_name_different_case_is_taken()
    {
        var client = factory.CreateClient();
        string name = ApiHelpers.NewName();
        await client.RegisterAsync(name);

        var again = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { username = name.ToUpperInvariant(), password = "password123", confirmPassword = "password123" });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("USERNAME_TAKEN", await again.CodeAsync());
    }

    [Theory]
    [InlineData("ab", "password123", "password123", "USERNAME_INVALID")]
    [InlineData("bad name!", "password123", "password123", "USERNAME_INVALID")]
    [InlineData("good_name", "short", "short", "PASSWORD_TOO_SHORT")]
    [InlineData("good_name", "password123", "password124", "PASSWORD_MISMATCH")]
    public async Task Register_rejects_bad_input_with_client_codes(string username, string password, string confirm, string code)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new { username, password, confirmPassword = confirm });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(code, await response.CodeAsync());
    }

    [Fact]
    public async Task Login_with_wrong_password_is_invalid_credentials()
    {
        var client = factory.CreateClient();
        string name = ApiHelpers.NewName();
        await client.RegisterAsync(name);

        var wrong = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = name, password = "nope12345" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal("INVALID_CREDENTIALS", await wrong.CodeAsync());

        var unknown = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "nobody_here", password = "x" });
        Assert.Equal("INVALID_CREDENTIALS", await unknown.CodeAsync());
    }

    [Fact]
    public async Task Malformed_json_returns_problem_400()
    {
        var content = new StringContent("not json", System.Text.Encoding.UTF8, "application/json");
        var response = await factory.CreateClient().PostAsync("/api/v1/auth/login", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("BAD_REQUEST", await response.CodeAsync());
    }

    [Fact]
    public async Task Missing_token_returns_problem_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/saves/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("UNAUTHORIZED", await response.CodeAsync());
    }
}
