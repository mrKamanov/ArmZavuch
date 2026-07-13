using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Подсказки для составления расписания: 1 класс и привязка к классам.</summary>
public sealed class Migration006TeacherClassPreferences : IMigration
{
    public int Version => 6;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await AddColumnIfMissingAsync(conn, "teachers", "works_with_first_grade", "INTEGER NOT NULL DEFAULT 0");

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS teacher_preferred_classes (
                teacher_id INTEGER NOT NULL REFERENCES teachers(id) ON DELETE CASCADE,
                class_id INTEGER NOT NULL REFERENCES school_classes(id) ON DELETE CASCADE,
                PRIMARY KEY (teacher_id, class_id)
            );
            CREATE INDEX IF NOT EXISTS idx_teacher_preferred_classes_class
                ON teacher_preferred_classes (class_id);
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task AddColumnIfMissingAsync(SqliteConnection conn, string table, string column, string ddl)
    {
        await using var check = conn.CreateCommand();
        check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = $n";
        check.Parameters.AddWithValue("$n", column);
        if (Convert.ToInt32(await check.ExecuteScalarAsync()) > 0)
            return;

        await using var alter = conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {ddl}";
        await alter.ExecuteNonQueryAsync();
    }
}
