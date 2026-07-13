using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Слот может содержать только предмет (этап A авторасписания) без педагога.</summary>
public sealed class Migration012NullableTeacherSlot : IMigration
{
    public int Version => 12;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await using var tx = await conn.BeginTransactionAsync();

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
                    subject_id INTEGER REFERENCES subjects(id),
                    teacher_id INTEGER REFERENCES teachers(id),
                    room_id INTEGER REFERENCES rooms(id),
                    subgroup_index INTEGER NOT NULL DEFAULT 0
                );
                INSERT INTO week_template_slots_new
                    (id, week_template_id, day_of_week, lesson_number, class_id, subject_id, teacher_id, room_id, subgroup_index)
                SELECT id, week_template_id, day_of_week, lesson_number, class_id, subject_id, teacher_id, room_id, subgroup_index
                FROM week_template_slots;
                DROP TABLE week_template_slots;
                ALTER TABLE week_template_slots_new RENAME TO week_template_slots;
                CREATE INDEX IF NOT EXISTS idx_week_template_slots_template
                    ON week_template_slots (week_template_id, day_of_week, class_id);
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
    }
}
