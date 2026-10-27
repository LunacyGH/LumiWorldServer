using System.Security.Claims;
using System.Text.Json;
using Lumi.Api.Features.Config;
using Lumi.Api.Features.Inbox;
using Lumi.Api.Infrastructure;
using Npgsql;

namespace Lumi.Api.Features.Obstacles;

public sealed record ObstacleRequest(string? Kind, int? Q, int? R);

/// <summary>
/// Gửi obstacle (giai đoạn 4). Client trừ phí Coins trước rồi gửi kèm Idempotency-Key;
/// server kiểm tra, đặt mục Obstacle vào hộp thư chủ đảo và không chạm vào ví của ai.
/// Từ chối thì client hoàn Coins, cùng cách với chợ.
/// Không có bảng mới: giới hạn đếm bằng inbox_items và action_log.
/// </summary>
public static class ObstacleEndpoints
{
    private static readonly TimeSpan DailyWindow = TimeSpan.FromHours(24);

    public static void MapObstacleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/worlds/{playerId:guid}/obstacles", SendObstacle).RequireAuthorization();
    }

    private static async Task<IResult> SendObstacle(
        Guid playerId, ObstacleRequest? body, ClaimsPrincipal user, HttpRequest http,
        NpgsqlDataSource db, GameConfigService config, CancellationToken ct)
    {
        string? opKey = OpKeys.ReadKey(http);
        if (opKey == null)
            return ApiProblem.Create(400, ErrorCodes.IdempotencyKeyMissing, "Send an Idempotency-Key header with this request.");

        string kind = (body?.Kind ?? string.Empty).Trim();
        if (kind.Length == 0 || body?.Q is not int q || body.R is not int r)
            return ApiProblem.Create(400, ErrorCodes.BadRequest, "Kind, q and r are required.");

        Guid senderId = user.PlayerId();
        if (senderId == playerId)
            return ApiProblem.Create(400, ErrorCodes.ObstacleOwnWorld, "You cannot send an obstacle to your own world.");

        GameConfig cfg = await config.GetAsync(ct);
        if (!cfg.ObstacleAllowedKinds.Contains(kind, StringComparer.Ordinal))
            return ApiProblem.Create(400, ErrorCodes.ObstacleKindNotAllowed, $"'{kind}' is not an obstacle you can send.");

        try
        {
            await using var conn = await db.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            // Khóa cả hai người chơi theo thứ tự id để hai chiều gửi cùng lúc không tự khóa nhau
            var players = await LockPlayersAsync(conn, tx, senderId, playerId, ct);
            if (!players.TryGetValue(playerId, out var target))
                return ApiProblem.Create(404, ErrorCodes.WorldNotFound, "This world does not exist.");
            if (!players.TryGetValue(senderId, out var sender))
                return ApiProblem.Create(401, ErrorCodes.Unauthorized, "Log in again to continue.");

            var stored = await OpKeys.FindAsync(conn, tx, senderId, opKey, ct);
            if (stored != null)
                return Replay(stored);

            string? snapshot = await ReadSnapshotAsync(conn, tx, playerId, ct);
            if (snapshot == null)
                return ApiProblem.Create(404, ErrorCodes.WorldNotFound, "This world is not shared.");

            // Người mới được bảo vệ: đảo của họ chưa nhận obstacle trong số giờ cấu hình
            TimeSpan protectedFor = TimeSpan.FromHours(cfg.ObstacleNewPlayerProtectionHours) - (DateTimeOffset.UtcNow - target.CreatedAt);
            if (protectedFor > TimeSpan.Zero)
                return ApiProblem.Create(409, ErrorCodes.ObstacleProtected,
                    $"This player is new. Their world is protected for {Math.Ceiling(protectedFor.TotalHours)} more hours.");

            if (!HasTile(snapshot, q, r))
                return ApiProblem.Create(400, ErrorCodes.ObstacleTileNotFound, "That tile is not in this world.");

            long sentToday = await CountSentTodayAsync(conn, tx, senderId, ct);
            if (sentToday >= cfg.ObstacleDailyLimit)
                return ApiProblem.Create(409, ErrorCodes.ObstacleDailyLimit,
                    $"You can send at most {cfg.ObstacleDailyLimit} obstacles in 24 hours.");

            long pending = await CountPendingAsync(conn, tx, playerId, ct);
            if (pending >= cfg.ObstacleMaxPendingPerWorld)
                return ApiProblem.Create(409, ErrorCodes.ObstaclePendingLimit,
                    "This world already has the most obstacles waiting. Try again after its owner logs in.");

            Guid obstacleId = Guid.NewGuid();
            await InboxEndpoints.InsertAsync(conn, tx, playerId, obstacleId, "Obstacle", new
            {
                obstacleId,
                kind,
                q,
                r,
                senderId = senderId.ToString(),
                senderName = sender.DisplayName,
                feeCoins = cfg.ObstacleFeeCoins
            }, ct);

            string json = JsonSerializer.Serialize(new { obstacleId, kind, q, r, feeCoins = cfg.ObstacleFeeCoins });

            await OpKeys.SaveAsync(conn, tx, senderId, opKey, 201, json, ct);
            await ActionLog.WriteAsync(conn, senderId, "obstacle.sent",
                new { targetId = playerId, obstacleId, kind, q, r, feeCoins = cfg.ObstacleFeeCoins }, tx, ct);
            await tx.CommitAsync(ct);

            return Results.Content(json, "application/json", statusCode: 201);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return await ReplayRaceAsync(db, senderId, opKey, ct);
        }
    }

    private sealed record PlayerRow(string DisplayName, DateTimeOffset CreatedAt);

    /// <summary>Khóa dòng người gửi và người nhận, luôn theo thứ tự id.</summary>
    private static async Task<Dictionary<Guid, PlayerRow>> LockPlayersAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, Guid a, Guid b, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "select id, display_name, created_at from players where id = any(@ids) and not is_suspended order by id for update", conn, tx);
        cmd.Parameters.AddWithValue("ids", new[] { a, b });

        var rows = new Dictionary<Guid, PlayerRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows[reader.GetGuid(0)] = new PlayerRow(reader.GetString(1), reader.GetFieldValue<DateTimeOffset>(2));
        return rows;
    }

    private static async Task<string?> ReadSnapshotAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid playerId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "select data::text from world_snapshots where player_id = @p", conn, tx);
        cmd.Parameters.AddWithValue("p", playerId);
        return (string?)await cmd.ExecuteScalarAsync(ct);
    }

    /// <summary>Ô (q, r) có trong snapshot không. Snapshot đã được kiểm tra khung khi đăng lên.</summary>
    private static bool HasTile(string snapshotJson, int q, int r)
    {
        using var doc = JsonDocument.Parse(snapshotJson);
        if (!doc.RootElement.TryGetProperty("tiles", out var tiles))
            return false;

        return tiles.EnumerateArray().Any(tile =>
            tile.TryGetProperty("q", out var tq) && tq.TryGetInt32(out int tileQ) && tileQ == q &&
            tile.TryGetProperty("r", out var tr) && tr.TryGetInt32(out int tileR) && tileR == r);
    }

    private static async Task<long> CountSentTodayAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid senderId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "select count(*) from action_log where player_id = @p and action = 'obstacle.sent' and at > now() - make_interval(hours => @h)", conn, tx);
        cmd.Parameters.AddWithValue("p", senderId);
        cmd.Parameters.AddWithValue("h", (int)DailyWindow.TotalHours);
        return (long)(await cmd.ExecuteScalarAsync(ct))!;
    }

    /// <summary>Obstacle đã gửi tới đảo mà chủ đảo chưa đăng nhập để nhận.</summary>
    private static async Task<long> CountPendingAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid targetId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "select count(*) from inbox_items where player_id = @p and kind = 'Obstacle' and acked_at is null", conn, tx);
        cmd.Parameters.AddWithValue("p", targetId);
        return (long)(await cmd.ExecuteScalarAsync(ct))!;
    }

    private static IResult Replay(OpKeys.StoredResponse stored) =>
        Results.Content(stored.ResponseJson ?? "{}", "application/json", statusCode: stored.StatusCode);

    /// <summary>Hai request cùng khóa chèn cùng lúc: request tới sau lấy kết quả của request tới trước.</summary>
    private static async Task<IResult> ReplayRaceAsync(NpgsqlDataSource db, Guid senderId, string opKey, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var stored = await OpKeys.FindAsync(conn, tx, senderId, opKey, ct);
        return stored != null
            ? Replay(stored)
            : ApiProblem.Create(409, ErrorCodes.OperationInProgress, "The same request is still being processed. Try again.");
    }
}
