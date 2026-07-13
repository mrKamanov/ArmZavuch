using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Целевой класс/урок для перестановок в day_overrides.</summary>
public sealed class Migration007DayOverrideTargets : IMigration
{
    public int Version => 7;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await AddColumnIfMissingAsync(conn, "day_overrides", "target_class_id", "INTEGER REFERENCES school_classes(id)");
        await AddColumnIfMissingAsync(conn, "day_overrides", "target_lesson_number", "INTEGER");
    }

    private static async Task AddColumnIfMissingAsync(SqliteConnection conn, string table, string column, string definition)
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
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
        await alter.ExecuteNonQueryAsync();
    }
}
