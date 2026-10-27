using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Lumi.Api.Tests;

[Collection("api")]
public sealed class WorldMapTests(LumiApiFactory factory)
{
    private static object Snapshot(int score) => new
    {
        version = 1,
        score,
        tiles = Enumerable.Range(0, 19).Select(i => new { q = i, r = 0, model = "Tile_Bloomfield_Lush" }),
        placements = new[] { new { cardID = "Leafwood", q = 0, r = 0, clusterId = 1 } }
    };

    private async Task<(ApiHelpers.Auth Auth, int X, int Y)> PublishAsync(string? username = null, int score = 10)
    {
        var owner = await factory.CreateClient().RegisterAsync(username);
        var body = await (await factory.As(owner).PutAsJsonAsync("/api/v1/worlds/me", Snapshot(score))).JsonAsync();
        return (owner, body.GetProperty("x").GetInt32(), body.GetProperty("y").GetInt32());
    }

    private static List<JsonElement> Items(JsonElement json, string name) =>
        json.GetProperty(name).EnumerateArray().ToList();

    [Fact]
    public async Task Island_keeps_its_coordinates_after_unshare_and_reshare()
    {
        var (owner, x, y) = await PublishAsync();
        Assert.InRange(x, 1, 10);
        Assert.True(y >= 1);

        var client = factory.As(owner);
        await client.DeleteAsync("/api/v1/worlds/me");
        var again = await (await client.PutAsJsonAsync("/api/v1/worlds/me", Snapshot(20))).JsonAsync();

        Assert.Equal(x, again.GetProperty("x").GetInt32());
        Assert.Equal(y, again.GetProperty("y").GetInt32());

        var one = await (await client.GetAsync($"/api/v1/worlds/{owner.PlayerId}")).JsonAsync();
        Assert.Equal(x, one.GetProperty("x").GetInt32());
        Assert.Equal(y, one.GetProperty("y").GetInt32());
    }

    [Fact]
    public async Task Two_islands_never_share_a_spot()
    {
        var a = await PublishAsync();
        var b = await PublishAsync();
        Assert.NotEqual((a.X, a.Y), (b.X, b.Y));
    }

    [Fact]
    public async Task Map_returns_shared_islands_in_the_region_only()
    {
        var (owner, x, y) = await PublishAsync();
        var (hidden, hx, hy) = await PublishAsync();
        await factory.As(hidden).DeleteAsync("/api/v1/worlds/me");
        var viewer = factory.As(await factory.CreateClient().RegisterAsync());

        var map = await (await viewer.GetAsync($"/api/v1/worlds/map?x={x}&y={y}&w=1&h=1")).JsonAsync();
        var islands = Items(map, "islands");
        Assert.Single(islands);
        Assert.Equal(owner.PlayerId, islands[0].GetProperty("playerId").GetString());
        Assert.Equal(10, map.GetProperty("columns").GetInt32());
        Assert.True(map.GetProperty("rows").GetInt32() >= y);

        // Đảo đã gỡ vẫn giữ ô nhưng không hiện trên bản đồ
        var empty = await (await viewer.GetAsync($"/api/v1/worlds/map?x={hx}&y={hy}&w=1&h=1")).JsonAsync();
        Assert.Empty(Items(empty, "islands"));
    }

    [Fact]
    public async Task Search_finds_islands_by_name_or_coordinates()
    {
        string tag = Guid.NewGuid().ToString("N")[..8];
        var (exact, x, y) = await PublishAsync("Lumi" + tag, score: 5);
        var (other, _, _) = await PublishAsync("x" + tag + "Lumi", score: 50);
        var viewer = factory.As(await factory.CreateClient().RegisterAsync());

        // Không phân biệt hoa thường; tên bắt đầu bằng từ khoá xếp trước dù điểm thấp hơn
        var byName = Items(await (await viewer.GetAsync($"/api/v1/worlds/search?q=LUMI{tag.ToUpperInvariant()}")).JsonAsync(), "items");
        Assert.Equal(exact.PlayerId, byName[0].GetProperty("playerId").GetString());

        var both = Items(await (await viewer.GetAsync($"/api/v1/worlds/search?q={tag}")).JsonAsync(), "items")
            .Select(i => i.GetProperty("playerId").GetString()).ToList();
        Assert.Contains(exact.PlayerId, both);
        Assert.Contains(other.PlayerId, both);

        var byCoords = Items(await (await viewer.GetAsync($"/api/v1/worlds/search?q=[{x}:{y}]")).JsonAsync(), "items");
        Assert.Single(byCoords);
        Assert.Equal(exact.PlayerId, byCoords[0].GetProperty("playerId").GetString());

        // Ký tự đại diện của LIKE được tìm theo nghĩa đen
        var wildcard = Items(await (await viewer.GetAsync("/api/v1/worlds/search?q=%25")).JsonAsync(), "items");
        Assert.Empty(wildcard);

        var blank = await viewer.GetAsync("/api/v1/worlds/search?q=%20");
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Equal("SEARCH_INVALID", await blank.CodeAsync());
    }
}
