using System.Net;
using System.Net.Http.Json;

namespace Lumi.Api.Tests;

[Collection("api")]
public sealed class WorldAndInboxTests(LumiApiFactory factory)
{
    private static object Snapshot(int score, int tileCount = 19, string padding = "") => new
    {
        version = 1,
        score,
        tiles = Enumerable.Range(0, tileCount).Select(i => new { q = i, r = 0, model = "Tile_Bloomfield_Lush" + padding }),
        placements = new[] { new { cardID = "Leafwood", q = 0, r = 0, clusterId = 1 } }
    };

    [Fact]
    public async Task Published_world_is_visible_to_other_players()
    {
        var owner = await factory.CreateClient().RegisterAsync();
        var visitor = await factory.CreateClient().RegisterAsync();

        var publish = await factory.As(owner).PutAsJsonAsync("/api/v1/worlds/me", Snapshot(42));
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        var gallery = await (await factory.As(visitor).GetAsync("/api/v1/worlds?sort=score&pageSize=50")).JsonAsync();
        Assert.Contains(gallery.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("playerId").GetString() == owner.PlayerId);

        var one = await (await factory.As(visitor).GetAsync($"/api/v1/worlds/{owner.PlayerId}")).JsonAsync();
        Assert.Equal(owner.DisplayName, one.GetProperty("displayName").GetString());
        Assert.Equal(42, one.GetProperty("score").GetInt32());
        Assert.Equal("Leafwood", one.GetProperty("snapshot").GetProperty("placements")[0].GetProperty("cardID").GetString());

        var removed = await factory.As(owner).DeleteAsync("/api/v1/worlds/me");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        var gone = await factory.As(visitor).GetAsync($"/api/v1/worlds/{owner.PlayerId}");
        Assert.Equal("WORLD_NOT_FOUND", await gone.CodeAsync());
    }

    [Fact]
    public async Task Snapshot_limits_are_enforced()
    {
        var owner = factory.As(await factory.CreateClient().RegisterAsync());

        var tooBig = await owner.PutAsJsonAsync("/api/v1/worlds/me", Snapshot(1, 400, new string('x', 900)));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooBig.StatusCode);
        Assert.Equal("SNAPSHOT_TOO_LARGE", await tooBig.CodeAsync());

        var tooManyTiles = await owner.PutAsJsonAsync("/api/v1/worlds/me", Snapshot(1, 501));
        Assert.Equal("SNAPSHOT_INVALID", await tooManyTiles.CodeAsync());

        var wrongVersion = await owner.PutAsJsonAsync("/api/v1/worlds/me",
            new { version = 2, tiles = new[] { 1 }, placements = Array.Empty<int>() });
        Assert.Equal("SNAPSHOT_VERSION_UNSUPPORTED", await wrongVersion.CodeAsync());
    }

    [Fact]
    public async Task Inbox_item_is_delivered_once_even_if_acked_twice()
    {
        var player = factory.As(await factory.CreateClient().RegisterAsync());

        var created = await (await player.PostAsync("/api/v1/inbox/dev-item", null)).JsonAsync();
        string id = created.GetProperty("id").GetString()!;

        var inbox = await (await player.GetAsync("/api/v1/inbox")).JsonAsync();
        var item = Assert.Single(inbox.GetProperty("items").EnumerateArray());
        Assert.Equal("System", item.GetProperty("kind").GetString());
        // payload là chuỗi JSON để client FromJson theo kind
        Assert.Contains("Test item", item.GetProperty("payload").GetString());

        var ack1 = await (await player.PostAsJsonAsync("/api/v1/inbox/ack", new { ids = new[] { id } })).JsonAsync();
        var ack2 = await (await player.PostAsJsonAsync("/api/v1/inbox/ack", new { ids = new[] { id } })).JsonAsync();
        Assert.Equal(1, ack1.GetProperty("acked").GetInt32());
        Assert.Equal(0, ack2.GetProperty("acked").GetInt32());

        var after = await (await player.GetAsync("/api/v1/inbox")).JsonAsync();
        Assert.Empty(after.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Config_has_defaults_without_login()
    {
        var config = await (await factory.CreateClient().GetAsync("/api/v1/config")).JsonAsync();
        Assert.Equal(5, config.GetProperty("marketFeePercent").GetInt32());
        Assert.Equal(2, config.GetProperty("obstacleAllowedKinds").GetArrayLength());
        Assert.Equal(60, config.GetProperty("syncInboxSeconds").GetInt32());
    }
}
