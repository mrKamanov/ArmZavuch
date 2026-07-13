using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Привязка педагога к строкам учебной нагрузки (curriculum).</summary>
public sealed class Migration015TeacherCurriculumItems : IMigration
{
    public int Version => 15;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS teacher_curriculum_items (
                teacher_id     INTEGER NOT NULL REFERENCES teachers(id),
                curriculum_id  INTEGER NOT NULL REFERENCES curriculum(id) ON DELETE CASCADE,
                PRIMARY KEY (teacher_id, curriculum_id)
            );
            CREATE INDEX IF NOT EXISTS idx_teacher_curriculum_teacher
                ON teacher_curriculum_items (teacher_id);
            CREATE INDEX IF NOT EXISTS idx_teacher_curriculum_item
                ON teacher_curriculum_items (curriculum_id);

            INSERT OR IGNORE INTO teacher_curriculum_items (teacher_id, curriculum_id)
            SELECT tcs.teacher_id, cu.id
            FROM teacher_class_subjects tcs
            JOIN curriculum cu ON cu.class_id = tcs.class_id AND cu.subject_id = tcs.subject_id;
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}
