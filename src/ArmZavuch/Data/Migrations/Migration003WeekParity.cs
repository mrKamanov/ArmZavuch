using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Чередование нагрузки: неделя А / неделя Б.</summary>
public sealed class Migration003WeekParity : IMigration
{
    public int Version => 3;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await AddColumnIfMissingAsync(conn, "curriculum", "week_parity", "TEXT NOT NULL DEFAULT 'EveryWeek'");
        await AddColumnIfMissingAsync(conn, "week_templates", "week_parity", "TEXT NOT NULL DEFAULT 'Any'");
    }

    private static async Task AddColumnIfMissingAsync(SqliteConnection conn, string table, string column, string def)
    {
        await using var check = conn.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table})";
        await using var reader = await check.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader.GetString(1).Equals(column, StringComparison.OrdinalIgnoreCase))
                return;
        }

        await using var alter = conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {def}";
        await alter.ExecuteNonQueryAsync();
    }
}
