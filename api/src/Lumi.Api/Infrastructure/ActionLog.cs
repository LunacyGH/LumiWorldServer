using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Lumi.Api.Infrastructure;

/// <summary>
/// Ghi một dòng vào action_log để làm dashboard và số liệu giữ chân (giai đoạn 5).
/// Truyền transaction khi muốn log chỉ được ghi nếu thao tác chính thành công.
/// </summary>
public static class ActionLog
{
    public static async Task WriteAsync(
        NpgsqlConnection conn,
        Guid? playerId,
        string action,
        object? details = null,
        NpgsqlTransaction? tx = null,
        CancellationToken ct = default)
    {
        await using var cmd = new NpgsqlCommand(
            "insert into action_log (player_id, action, details) values (@p, @a, @d)", conn, tx);

        cmd.Parameters.AddWithValue("p", (object?)playerId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("a", action);
        cmd.Parameters.Add(new NpgsqlParameter("d", NpgsqlDbType.Jsonb)
        {
            Value = details == null ? DBNull.Value : JsonSerializer.Serialize(details)
        });

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
