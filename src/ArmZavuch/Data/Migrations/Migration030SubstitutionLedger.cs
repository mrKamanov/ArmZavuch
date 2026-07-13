using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Журнал замен для отчётов и ручного учёта (официально / неофициально).</summary>
public sealed class Migration030SubstitutionLedger : IMigration
{
    public int Version => 30;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS substitution_records (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                date TEXT NOT NULL,
                lesson_number INTEGER NOT NULL,
                class_id INTEGER REFERENCES school_classes(id),
                class_name TEXT NOT NULL DEFAULT '',
                subject_id INTEGER REFERENCES subjects(id),
                subject_name TEXT NOT NULL DEFAULT '',
                absent_teacher_id INTEGER NOT NULL REFERENCES teachers(id),
                absent_teacher_name TEXT NOT NULL DEFAULT '',
                replacement_teacher_id INTEGER NOT NULL REFERENCES teachers(id),
                replacement_teacher_name TEXT NOT NULL DEFAULT '',
                start_time TEXT NOT NULL DEFAULT '',
                end_time TEXT NOT NULL DEFAULT '',
                is_official INTEGER NOT NULL DEFAULT 1,
                source TEXT NOT NULL DEFAULT 'dispatcher',
                note TEXT,
                day_override_id INTEGER,
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE INDEX IF NOT EXISTS idx_substitution_records_date
                ON substitution_records (date);
            CREATE INDEX IF NOT EXISTS idx_substitution_records_replacement
                ON substitution_records (replacement_teacher_id, date);
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}
