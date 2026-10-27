using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Lumi.Api.Infrastructure;
using Npgsql;
using NpgsqlTypes;

namespace Lumi.Api.Features.Inbox;

public sealed record AckRequest(List<string>? Ids);

/// <summary>
/// Hộp thư: điểm nối giữa hai người chơi. Client nhận mục, áp vào save, ghi id mục vào save
/// rồi mới ack. Ack lỗi thì lần sau client thấy id đã có trong save và chỉ ack lại.
/// </summary>
public static class InboxEndpoints
{
    private const int MaxItemsPerFetch = 100;

    public static void MapInboxEndpoints(this IEndpointRouteBuilder app, IWebHostEnvironment env)
    {
        var group = app.MapGroup("/api/v1/inbox").RequireAuthorization();
        group.MapGet("/", GetPending);
        group.MapPost("/ack", Ack);

        // Chỉ có khi chạy dev hoặc test: tự gửi một mục System cho mình để thử luồng nhận và ack
        if (env.IsDevelopment() || env.IsEnvironment("Testing"))
            group.MapPost("/dev-item", AddDevItem);
    }

    /// <summary>
    /// payload trả về dạng chuỗi JSON: JsonUtility không đọc được object khác kiểu theo kind,
    /// nên client đọc kind trước rồi FromJson payload theo đúng lớp.
    /// </summary>
    private static async Task<IResult> GetPending(ClaimsPrincipal user, NpgsqlDataSource db, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            select id, kind, payload::text, created_at from inbox_items
            where player_id = @p and acked_at is null
            order by created_at
            limit @limit
            """, conn);
        cmd.Parameters.AddWithValue("p", user.PlayerId());
        cmd.Parameters.AddWithValue("limit", MaxItemsPerFetch);

        var items = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new
            {
                id = reader.GetGuid(0).ToString(),
                kind = reader.GetString(1),
                payload = reader.GetString(2),
                createdAt = reader.GetFieldValue<DateTimeOffset>(3).ToString("o", CultureInfo.InvariantCulture)
            });
        }

        return Results.Ok(new { items });
    }

    /// <summary>Ack nhiều lần cùng một id không sao: chỉ mục chưa ack mới được tính.</summary>
    private static async Task<IResult> Ack(ClaimsPrincipal user, AckRequest body, NpgsqlDataSource db, CancellationToken ct)
    {
        var ids = (body.Ids ?? [])
            .Select(raw => Guid.TryParse(raw, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
            return Results.Ok(new { acked = 0 });

        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "update inbox_items set acked_at = now() where player_id = @p and acked_at is null and id = any(@ids)", conn);
        cmd.Parameters.AddWithValue("p", user.PlayerId());
        cmd.Parameters.AddWithValue("ids", ids);

        int acked = await cmd.ExecuteNonQueryAsync(ct);
        return Results.Ok(new { acked });
    }

    private static async Task<IResult> AddDevItem(ClaimsPrincipal user, NpgsqlDataSource db, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await using var conn = await db.OpenConnectionAsync(ct);
        await InsertAsync(conn, null, user.PlayerId(), id, "System", new { message = "Test item from the server" }, ct);
        return Results.Ok(new { id = id.ToString() });
    }

    /// <summary>Các tính năng sau (chợ, obstacle) gọi hàm này trong transaction của chúng.</summary>
    public static async Task InsertAsync(
        NpgsqlConnection conn, NpgsqlTransaction? tx, Guid playerId, Guid itemId, string kind, object payload,
        CancellationToken ct = default)
    {
        await using var cmd = new NpgsqlCommand(
            "insert into inbox_items (id, player_id, kind, payload) values (@id, @p, @k, @d)", conn, tx);
        cmd.Parameters.AddWithValue("id", itemId);
        cmd.Parameters.AddWithValue("p", playerId);
        cmd.Parameters.AddWithValue("k", kind);
        cmd.Parameters.Add(new NpgsqlParameter("d", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(payload) });
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
