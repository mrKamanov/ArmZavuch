using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Единые записи отсутствия: открытый период, источник, официальность; миграция TeacherAbsent.</summary>
public sealed class Migration029TeacherAbsenceUnify : IMigration
{
    public int Version => 29;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await using var tx = await conn.BeginTransactionAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = (SqliteTransaction)tx;
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS teacher_status_periods_new (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    teacher_id INTEGER NOT NULL REFERENCES teachers(id) ON DELETE CASCADE,
                    status_type TEXT NOT NULL,
                    start_date TEXT NOT NULL,
                    end_date TEXT,
                    note TEXT,
                    is_official INTEGER NOT NULL DEFAULT 0,
                    source TEXT NOT NULL DEFAULT 'profile'
                );
                INSERT INTO teacher_status_periods_new
                    (id, teacher_id, status_type, start_date, end_date, note, is_official, source)
                SELECT id, teacher_id, status_type, start_date, end_date, note, 0, 'profile'
                FROM teacher_status_periods;
                DROP TABLE teacher_status_periods;
                ALTER TABLE teacher_status_periods_new RENAME TO teacher_status_periods;
                CREATE INDEX IF NOT EXISTS idx_status_teacher_dates
                    ON teacher_status_periods (teacher_id, start_date, end_date);
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = (SqliteTransaction)tx;
            cmd.CommandText = """
                INSERT INTO teacher_status_periods
                    (teacher_id, status_type, start_date, end_date, note, is_official, source)
                SELECT
                    teacher_id,
                    CASE
                        WHEN note LIKE 'Больничный%' THEN 'Sick'
                        WHEN note LIKE 'Отгул%' THEN 'Leave'
                        ELSE 'Other'
                    END,
                    date,
                    date,
                    note,
                    0,
                    'dispatcher'
                FROM day_overrides
                WHERE override_type = 'TeacherAbsent' AND teacher_id IS NOT NULL;
                DELETE FROM day_overrides WHERE override_type = 'TeacherAbsent';
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
    }
}
