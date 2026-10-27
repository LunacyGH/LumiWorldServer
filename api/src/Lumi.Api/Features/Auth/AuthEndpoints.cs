using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Lumi.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Npgsql;

namespace Lumi.Api.Features.Auth;

public static partial class AuthEndpoints
{
    // Cùng luật với AuthScreen.cs phía client
    [GeneratedRegex("^[A-Za-z0-9_]{3,20}$")]
    private static partial Regex UsernamePattern();

    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 128;

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth");

        group.MapPost("/register", Register).RequireRateLimiting("auth");
        group.MapPost("/login", Login).RequireRateLimiting("auth");
        group.MapGet("/me", Me).RequireAuthorization();
    }

    private static async Task<IResult> Register(
        RegisterRequest body,
        NpgsqlDataSource db,
        TokenService tokens,
        IPasswordHasher<PlayerAccount> hasher,
        CancellationToken ct)
    {
        string username = body.Username?.Trim() ?? "";
        string password = body.Password ?? "";

        if (!UsernamePattern().IsMatch(username))
            return ApiProblem.Create(400, ErrorCodes.UsernameInvalid,
                "Usernames must be 3-20 characters, using only letters, numbers and underscores.");

        if (password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
            return ApiProblem.Create(400, ErrorCodes.PasswordTooShort, "Passwords must be at least 8 characters.");

        if (password != body.ConfirmPassword)
            return ApiProblem.Create(400, ErrorCodes.PasswordMismatch, "The passwords don't match.");

        var playerId = Guid.NewGuid();
        string hash = hasher.HashPassword(new PlayerAccount(), password);

        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // on conflict do nothing: hai người đăng ký cùng tên một lúc thì một người nhận USERNAME_TAKEN
        await using (var insert = new NpgsqlCommand(
            """
            insert into players (id, username, username_normalized, display_name, password_hash)
            values (@id, @u, @n, @u, @h)
            on conflict (username_normalized) do nothing
            """, conn, tx))
        {
            insert.Parameters.AddWithValue("id", playerId);
            insert.Parameters.AddWithValue("u", username);
            insert.Parameters.AddWithValue("n", username.ToLowerInvariant());
            insert.Parameters.AddWithValue("h", hash);

            if (await insert.ExecuteNonQueryAsync(ct) == 0)
                return ApiProblem.Create(409, ErrorCodes.UsernameTaken, "That username is already taken.");
        }

        await ActionLog.WriteAsync(conn, playerId, "register", tx: tx, ct: ct);
        await tx.CommitAsync(ct);

        return Results.Ok(CreateResponse(tokens, playerId, username, username, "player"));
    }

    private static async Task<IResult> Login(
        LoginRequest body,
        NpgsqlDataSource db,
        TokenService tokens,
        IPasswordHasher<PlayerAccount> hasher,
        CancellationToken ct)
    {
        string username = body.Username?.Trim() ?? "";
        string password = body.Password ?? "";

        await using var conn = await db.OpenConnectionAsync(ct);

        Guid playerId;
        string storedName, displayName, hash, role;
        bool suspended;

        await using (var find = new NpgsqlCommand(
            "select id, username, display_name, password_hash, role, is_suspended from players where username_normalized = @n",
            conn))
        {
            find.Parameters.AddWithValue("n", username.ToLowerInvariant());
            await using var reader = await find.ExecuteReaderAsync(ct);

            if (!await reader.ReadAsync(ct))
                return InvalidCredentials();

            playerId = reader.GetGuid(0);
            storedName = reader.GetString(1);
            displayName = reader.GetString(2);
            hash = reader.GetString(3);
            role = reader.GetString(4);
            suspended = reader.GetBoolean(5);
        }

        var check = hasher.VerifyHashedPassword(new PlayerAccount(), hash, password);
        if (check == PasswordVerificationResult.Failed)
            return InvalidCredentials();

        // Báo bị khóa chỉ sau khi đúng mật khẩu, để người lạ không dò được tài khoản nào bị khóa
        if (suspended)
            return ApiProblem.Create(403, ErrorCodes.AccountSuspended, "This account can't be used right now.");

        await using (var update = new NpgsqlCommand(
            "update players set last_login_at = now(), password_hash = coalesce(@h, password_hash) where id = @id", conn))
        {
            // Hash cũ (thuật toán yếu hơn) thì băm lại bằng thuật toán hiện tại
            string? rehash = check == PasswordVerificationResult.SuccessRehashNeeded
                ? hasher.HashPassword(new PlayerAccount(), password)
                : null;

            update.Parameters.AddWithValue("h", (object?)rehash ?? DBNull.Value);
            update.Parameters.AddWithValue("id", playerId);
            await update.ExecuteNonQueryAsync(ct);
        }

        await ActionLog.WriteAsync(conn, playerId, "login", ct: ct);

        return Results.Ok(CreateResponse(tokens, playerId, storedName, displayName, role));
    }

    private static async Task<IResult> Me(ClaimsPrincipal user, NpgsqlDataSource db, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "select username, display_name, role, created_at from players where id = @id", conn);
        cmd.Parameters.AddWithValue("id", user.PlayerId());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return ApiProblem.Create(401, ErrorCodes.Unauthorized, "This account no longer exists.");

        return Results.Ok(new MeResponse(
            user.PlayerId().ToString(),
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetFieldValue<DateTimeOffset>(3).ToString("o", CultureInfo.InvariantCulture)));
    }

    private static IResult InvalidCredentials() =>
        ApiProblem.Create(401, ErrorCodes.InvalidCredentials, "Incorrect username or password.");

    private static AuthResponse CreateResponse(TokenService tokens, Guid playerId, string username, string displayName, string role)
    {
        var (token, expiresAt) = tokens.Issue(playerId, username, role);
        return new AuthResponse(
            playerId.ToString(),
            username,
            displayName,
            token,
            expiresAt.ToString("o", CultureInfo.InvariantCulture));
    }
}
