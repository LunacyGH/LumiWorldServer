using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace Lumi.Api.Tests;

/// <summary>
/// Dựng server trong bộ nhớ, nối vào database test riêng lumi_test trên máy.
/// Mỗi lần khởi tạo: lumi_owner xóa sạch schema rồi chạy các file trong $LUMI_DB_DIR\migrations
/// (mặc định D:\LumiDB) giống scripts\migrate.ps1; server trong test nối bằng lumi_app như server thật.
/// Đổi chuỗi kết nối bằng biến môi trường LUMI_TEST_OWNER_DB và LUMI_TEST_DB.
/// </summary>
public sealed class LumiApiFactory : WebApplicationFactory<Program>
{
    public static readonly string OwnerConnectionString =
        Environment.GetEnvironmentVariable("LUMI_TEST_OWNER_DB")
        ?? "Host=localhost;Port=5432;Database=lumi_test;Username=lumi_owner;Password=" + Environment.GetEnvironmentVariable("LUMI_OWNER_PASSWORD");

    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("LUMI_TEST_DB")
        ?? "Host=localhost;Port=5432;Database=lumi_test;Username=lumi_app;Password=" + Environment.GetEnvironmentVariable("LUMI_APP_PASSWORD");

    public static readonly string MigrationsDir = Path.Combine(
        Environment.GetEnvironmentVariable("LUMI_DB_DIR") ?? @"D:\LumiDB", "migrations");

    public LumiApiFactory()
    {
        using var conn = new NpgsqlConnection(OwnerConnectionString);
        conn.Open();

        // Schema public tạo lại thuộc lumi_owner; quyền trên bảng mới đến từ default privileges trong setup\03_grants.sql
        Execute(conn, null, """
            drop schema public cascade;
            create schema public;
            revoke create on schema public from public;
            grant usage on schema public to lumi_app, lumi_report;
            create table schema_migrations (version text primary key, applied_at timestamptz not null default now());
            """);

        if (!Directory.Exists(MigrationsDir))
            throw new DirectoryNotFoundException($"Không thấy {MigrationsDir}. Đặt biến môi trường LUMI_DB_DIR.");

        foreach (string file in Directory.GetFiles(MigrationsDir, "*.sql").OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal))
        {
            using var tx = conn.BeginTransaction();
            Execute(conn, tx, File.ReadAllText(file));
            using (var mark = new NpgsqlCommand("insert into schema_migrations (version) values (@v)", conn, tx))
            {
                mark.Parameters.AddWithValue("v", Path.GetFileNameWithoutExtension(file));
                mark.ExecuteNonQuery();
            }
            tx.Commit();
        }
    }

    private static void Execute(NpgsqlConnection conn, NpgsqlTransaction? tx, string sql)
    {
        using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.ExecuteNonQuery();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Lumi", ConnectionString);
        builder.UseSetting("Jwt:Key", "test-only-key-0123456789abcdef0123456789");
        builder.UseSetting("RateLimit:AuthPerMinute", "1000");
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<LumiApiFactory>;
