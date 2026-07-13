using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Связи учитель↔класс↔предмет (для подсказок, автоназначения и замен).</summary>
public sealed class Migration014TeacherClassSubjects : IMigration
{
    public int Version => 14;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS teacher_class_subjects (
                teacher_id INTEGER NOT NULL REFERENCES teachers(id),
                class_id   INTEGER NOT NULL REFERENCES school_classes(id),
                subject_id INTEGER NOT NULL REFERENCES subjects(id),
                PRIMARY KEY (teacher_id, class_id, subject_id)
            );
            CREATE INDEX IF NOT EXISTS idx_teacher_class_subjects_teacher
                ON teacher_class_subjects (teacher_id);
            CREATE INDEX IF NOT EXISTS idx_teacher_class_subjects_class_subject
                ON teacher_class_subjects (class_id, subject_id);
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}

