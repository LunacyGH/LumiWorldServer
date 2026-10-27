using System.Net;
using System.Net.Http.Json;

namespace Lumi.Api.Tests;

[Collection("api")]
public sealed class SaveTests(LumiApiFactory factory)
{
    private static object Body(long baseRevision, int coins) => new
    {
        baseRevision,
        world = new { version = 1, tiles = new[] { new { q = 0, r = 0, model = "Tile_Leafwood_Lush" } } },
        economy = new { version = 1, coins }
    };

    [Fact]
    public async Task One_save_per_player_with_revision_check()
    {
        var player = await factory.CreateClient().RegisterAsync();
        var client = factory.As(player);

        var empty = await client.GetAsync("/api/v1/saves/me");
        Assert.Equal(HttpStatusCode.NotFound, empty.StatusCode);
        Assert.Equal("SAVE_NOT_FOUND", await empty.CodeAsync());

        var first = await client.PutAsJsonAsync("/api/v1/saves/me", Body(0, 100));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(1, (await first.JsonAsync()).GetProperty("revision").GetInt64());

        // Máy thứ hai vẫn nghĩ chưa có save: bị từ chối, không ghi đè
        var stale = await client.PutAsJsonAsync("/api/v1/saves/me", Body(0, 999));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var conflict = await stale.JsonAsync();
        Assert.Equal("SAVE_CONFLICT", conflict.GetProperty("code").GetString());
        Assert.Equal(1, conflict.GetProperty("currentRevision").GetInt64());

        var second = await client.PutAsJsonAsync("/api/v1/saves/me", Body(1, 150));
        Assert.Equal(2, (await second.JsonAsync()).GetProperty("revision").GetInt64());

        var loaded = await (await client.GetAsync("/api/v1/saves/me")).JsonAsync();
        Assert.Equal(2, loaded.GetProperty("revision").GetInt64());
        Assert.Equal(150, loaded.GetProperty("economy").GetProperty("coins").GetInt32());
        Assert.Equal("Tile_Leafwood_Lush",
            loaded.GetProperty("world").GetProperty("tiles")[0].GetProperty("model").GetString());
    }

    [Fact]
    public async Task Players_never_see_each_others_save()
    {
        var a = await factory.CreateClient().RegisterAsync();
        var b = await factory.CreateClient().RegisterAsync();

        await factory.As(a).PutAsJsonAsync("/api/v1/saves/me", Body(0, 100));

        var bSave = await factory.As(b).GetAsync("/api/v1/saves/me");
        Assert.Equal(HttpStatusCode.NotFound, bSave.StatusCode);
    }

    [Fact]
    public async Task Save_without_world_version_is_rejected()
    {
        var player = await factory.CreateClient().RegisterAsync();
        var response = await factory.As(player).PutAsJsonAsync("/api/v1/saves/me",
            new { baseRevision = 0, world = new { tiles = Array.Empty<int>() }, economy = new { coins = 1 } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("SAVE_INVALID", await response.CodeAsync());
    }
}
