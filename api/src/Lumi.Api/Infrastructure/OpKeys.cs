using Npgsql;
using NpgsqlTypes;

namespace Lumi.Api.Infrastructure;

/// <summary>
/// Chống làm lại một thao tác khi client gửi lại vì mất mạng (header Idempotency-Key).
/// Dùng trong cùng transaction với thao tác: tìm trước, nếu có thì trả kết quả cũ;
/// nếu chưa có thì làm thao tác rồi lưu kết quả trước khi commit.
/// Chợ (giai đoạn 1) và gửi obstacle (giai đoạn 4) dùng lớp này.
/// </summary>
public static class OpKeys
{
    public const string HeaderName = "Idempotency-Key";
    public const int MaxLength = 64;

    public sealed record StoredResponse(int StatusCode, string? ResponseJson);

    /// <summary>Đọc header; trả null nếu thiếu hoặc dài quá.</summary>
    public static string? ReadKey(HttpRequest request)
    {
        string? key = request.Headers[HeaderName];
        if (string.IsNullOrWhiteSpace(key) || key.Length > MaxLength)
            return null;
        return key.Trim();
    }

    /// <summary>Khóa dòng op key (nếu có) tới hết transaction để hai request trùng không chạy song song.</summary>
    public static async Task<StoredResponse?> FindAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, Guid playerId, string opKey, CancellationToken ct = default)
    {
        await using var cmd = new NpgsqlCommand(
            "select status_code, response::text from op_keys where player_id = @p and op_key = @k for update", conn, tx);
        cmd.Parameters.AddWithValue("p", playerId);
        cmd.Parameters.AddWithValue("k", opKey);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new StoredResponse(reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    public static async Task SaveAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, Guid playerId, string opKey,
        int statusCode, string? responseJson, CancellationToken ct = default)
    {
        await using var cmd = new NpgsqlCommand(
            "insert into op_keys (player_id, op_key, status_code, response) values (@p, @k, @s, @r)", conn, tx);
        cmd.Parameters.AddWithValue("p", playerId);
        cmd.Parameters.AddWithValue("k", opKey);
        cmd.Parameters.AddWithValue("s", statusCode);
        cmd.Parameters.Add(new NpgsqlParameter("r", NpgsqlDbType.Jsonb) { Value = (object?)responseJson ?? DBNull.Value });
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
