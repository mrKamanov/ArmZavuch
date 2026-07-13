using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Кабинет/зал физкультуры у класса; параллельные группы в спортзале.</summary>
public sealed class Migration013PeRoomAndHallSharing : IMigration
{
    public int Version => 13;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await AddColumnIfMissingAsync(conn, "school_classes", "default_pe_room_id", "INTEGER REFERENCES rooms(id)");
        await AddColumnIfMissingAsync(conn, "rooms", "allows_parallel_groups", "INTEGER NOT NULL DEFAULT 0");
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
