using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Явная привязка класса к зданию (для раскладки в конструкторе).</summary>
public sealed class Migration017ClassBuilding : IMigration
{
    public int Version => 17;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await AddColumnIfMissingAsync(conn, "school_classes", "building_id", "INTEGER REFERENCES buildings(id)");

        await using var backfill = conn.CreateCommand();
        backfill.CommandText = """
            UPDATE school_classes
            SET building_id = (
                SELECT r.building_id FROM rooms r WHERE r.id = school_classes.default_room_id
            )
            WHERE building_id IS NULL AND default_room_id IS NOT NULL;
            """;
        await backfill.ExecuteNonQueryAsync();
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
