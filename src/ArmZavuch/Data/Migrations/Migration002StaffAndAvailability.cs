using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Контакты сотрудников, статусы отсутствия, нерабочее время.</summary>
public sealed class Migration002StaffAndAvailability : IMigration
{
    public int Version => 2;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await AddColumnIfMissingAsync(conn, "teachers", "job_title", "TEXT");
        await AddColumnIfMissingAsync(conn, "teachers", "phone", "TEXT");
        await AddColumnIfMissingAsync(conn, "teachers", "contact_url", "TEXT");
        await AddColumnIfMissingAsync(conn, "teachers", "contact_note", "TEXT");

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS teacher_status_periods (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                teacher_id INTEGER NOT NULL REFERENCES teachers(id) ON DELETE CASCADE,
                status_type TEXT NOT NULL,
                start_date TEXT NOT NULL,
                end_date TEXT NOT NULL,
                note TEXT
            );

            CREATE TABLE IF NOT EXISTS teacher_unavailability (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                teacher_id INTEGER NOT NULL REFERENCES teachers(id) ON DELETE CASCADE,
                recurrence_type TEXT NOT NULL,
                day_of_week INTEGER,
                start_date TEXT NOT NULL,
                end_date TEXT,
                all_day INTEGER NOT NULL DEFAULT 1,
                lesson_from INTEGER,
                lesson_to INTEGER,
                note TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_status_teacher_dates
                ON teacher_status_periods (teacher_id, start_date, end_date);
            CREATE INDEX IF NOT EXISTS idx_unavail_teacher
                ON teacher_unavailability (teacher_id);
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task AddColumnIfMissingAsync(SqliteConnection conn, string table, string column, string type)
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
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {type}";
        await alter.ExecuteNonQueryAsync();
    }
}
