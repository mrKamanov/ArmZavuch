using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Кабинет класса по умолчанию; необязательный кабинет в слоте (дин. пауза).</summary>
public sealed class Migration009ClassDefaultRoom : IMigration
{
    public int Version => 9;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await using var tx = await conn.BeginTransactionAsync();

        await AddColumnIfMissingAsync(conn, "school_classes", "default_room_id", "INTEGER REFERENCES rooms(id)");
        await AddColumnIfMissingAsync(conn, "day_overrides", "clear_room", "INTEGER NOT NULL DEFAULT 0");

        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = (SqliteTransaction)tx;
            cmd.CommandText = """
                DROP TABLE IF EXISTS week_template_slots_new;
                CREATE TABLE week_template_slots_new (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    week_template_id INTEGER NOT NULL REFERENCES week_templates(id),
                    day_of_week INTEGER NOT NULL,
                    lesson_number INTEGER NOT NULL,
                    class_id INTEGER NOT NULL REFERENCES school_classes(id),
                    subject_id INTEGER NOT NULL REFERENCES subjects(id),
                    teacher_id INTEGER NOT NULL REFERENCES teachers(id),
                    room_id INTEGER REFERENCES rooms(id),
                    subgroup_index INTEGER NOT NULL DEFAULT 0
                );
                INSERT INTO week_template_slots_new
                    (id, week_template_id, day_of_week, lesson_number, class_id, subject_id, teacher_id, room_id, subgroup_index)
                SELECT id, week_template_id, day_of_week, lesson_number, class_id, subject_id, teacher_id, room_id, subgroup_index
                FROM week_template_slots;
                DROP TABLE week_template_slots;
                ALTER TABLE week_template_slots_new RENAME TO week_template_slots;
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
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
