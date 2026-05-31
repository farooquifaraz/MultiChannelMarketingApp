using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MarketingApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MarketingApp.Tests.Regression;

/// <summary>
/// K1 Zero-Regression harness — boots the real API in-process via WebApplicationFactory,
/// pointed at a dedicated test Postgres database. Each test run gets a fresh DB
/// (drop + recreate + idempotent startup migrations rebuild all 18 tables).
///
/// Why a real Postgres and not SQLite/in-memory?
///   The codebase relies on Postgres-specific SQL — ILIKE for case-insensitive
///   search, gen_random_uuid(), DO $$ BEGIN ... blocks in the migration array.
///   SQLite would silently miss bugs around those.
///
/// Connection-string precedence:
///   1. env var TEST_POSTGRES_CONNECTION (CI / custom dev)
///   2. fallback: Host=localhost;Port=5432;Database=marketingapp_test;
///      Username=postgres;Password=yourpassword;
///
/// CI runners spin a Postgres service container and export the env var.
/// Local devs need only their existing dev Postgres on 5432.
/// </summary>
public class RegressionTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DefaultConnection =
        "Host=localhost;Port=5432;Database=marketingapp_test;Username=postgres;Password=yourpassword;Pooling=true;MinPoolSize=2;MaxPoolSize=10";

    public string ConnectionString { get; }

    public RegressionTestFactory()
    {
        ConnectionString =
            Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION")
            ?? DefaultConnection;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production"); // run the same startup migrations real prod runs

        // Override the connection string + a few JWT/CORS values the app needs to boot.
        builder.ConfigureAppConfiguration((ctx, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                // tests don't use Redis — point at an unused host so the app doesn't try to talk to a real one
                ["ConnectionStrings:Redis"] = "localhost:65535,abortConnect=false",
                ["Jwt:SecretKey"] = "K1-REGRESSION-HARNESS-JWT-SECRET-AT-LEAST-32-CHARS-LONG-XXX",
                ["Jwt:Issuer"] = "MarketingApp.Tests",
                ["Jwt:Audience"] = "MarketingApp.Tests",
                ["Jwt:ExpiryMinutes"] = "60",
                ["Jwt:RefreshTokenExpiryDays"] = "30",
                ["AllowedOrigins"] = "http://localhost",
            });
        });
    }

    public async Task InitializeAsync()
    {
        // Wipe + recreate the test database so every test run starts clean.
        // Direct ADO so we never touch the API's connection pool here.
        var adminConn = ConnectionString.Replace(
            "Database=marketingapp_test", "Database=postgres");

        await using (var conn = new Npgsql.NpgsqlConnection(adminConn))
        {
            await conn.OpenAsync();
            // Drop existing test DB (terminates any other connections to it first).
            await ExecAsync(conn,
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity " +
                "WHERE datname = 'marketingapp_test' AND pid <> pg_backend_pid();");
            await ExecAsync(conn, "DROP DATABASE IF EXISTS marketingapp_test;");
            await ExecAsync(conn, "CREATE DATABASE marketingapp_test;");
        }

        // First HTTP request triggers Program.cs startup migrations, creating
        // all 18 tables on the fresh DB.
        using var http = CreateClient();
        var health = await http.GetAsync("/health");
        if (!health.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Test API did not become healthy: {health.StatusCode} — " +
                $"check that local Postgres is running on 5432 with password 'yourpassword'.");
    }

    public new Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Truncate the users table so a test can assert "fresh DB" behaviour
    /// (e.g. BUG-001 first-user-becomes-admin) regardless of what other tests in the
    /// same collection have created. CASCADE drops dependent rows in one shot.</summary>
    public async Task TruncateUsersAsync()
    {
        await using var conn = new Npgsql.NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await ExecAsync(conn, "TRUNCATE TABLE users CASCADE;");
    }

    private static async Task ExecAsync(Npgsql.NpgsqlConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    // ---- Helpers that test classes use to keep noise out of the test methods ----

    /// <summary>Send JSON to an endpoint with an optional bearer token; deserialise the typed `data`.</summary>
    public async Task<ApiEnvelope<T>?> PostJsonAsync<T>(
        string url, object body, string? jwt = null)
    {
        using var http = CreateClient();
        if (jwt is not null)
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var content = new StringContent(
            JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var resp = await http.PostAsync(url, content);
        var raw = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiEnvelope<T>>(raw, JsonOpts);
    }

    public async Task<(System.Net.HttpStatusCode status, string body)> PostRawAsync(
        string url, object body, string? jwt = null)
    {
        using var http = CreateClient();
        if (jwt is not null)
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var content = new StringContent(
            JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var resp = await http.PostAsync(url, content);
        return (resp.StatusCode, await resp.Content.ReadAsStringAsync());
    }

    public async Task<(System.Net.HttpStatusCode status, string body)> GetRawAsync(
        string url, string? jwt = null)
    {
        using var http = CreateClient();
        if (jwt is not null)
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var resp = await http.GetAsync(url);
        return (resp.StatusCode, await resp.Content.ReadAsStringAsync());
    }

    public async Task<T?> GetJsonAsync<T>(string url, string? jwt = null)
    {
        var (_, body) = await GetRawAsync(url, jwt);
        return JsonSerializer.Deserialize<T>(body, JsonOpts);
    }

    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}

// Matches src/MarketingApp.Application/DTOs/ApiResponse — kept here so the
// test project doesn't have to depend on the API's internal envelope type.
public record ApiEnvelope<T>(bool Success, string? Message, T? Data, List<string>? Errors);
