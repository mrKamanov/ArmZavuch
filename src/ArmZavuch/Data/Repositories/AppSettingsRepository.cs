using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Repositories;

/// <summary>Ключ-значение настроек приложения (app_settings).</summary>
public sealed class AppSettingsRepository
{
    private readonly SqliteConnectionFactory _factory;

    public AppSettingsRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<string?> GetAsync(string key)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM app_settings WHERE key = $k";
        cmd.Parameters.AddWithValue("$k", key);
        return await cmd.ExecuteScalarAsync() as string;
    }

    public async Task SetAsync(string key, string value)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO app_settings (key, value) VALUES ($k, $v)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value
            """;
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        await cmd.ExecuteNonQueryAsync();
    }
}
