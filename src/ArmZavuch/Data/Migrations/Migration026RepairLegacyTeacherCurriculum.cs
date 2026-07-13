using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>
/// Восстанавливает привязки нагрузки из устаревшей teacher_class_subjects в teacher_curriculum_items.
/// Вход: строки legacy без пары в tci. Выход: явные назначения для анкеты и таблицы нагрузки.
/// </summary>
public sealed class Migration026RepairLegacyTeacherCurriculum : IMigration
{
    public int Version => 26;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO teacher_curriculum_items (teacher_id, curriculum_id, source)
            SELECT tcs.teacher_id, cu.id, 'explicit'
            FROM teacher_class_subjects tcs
            JOIN curriculum cu ON cu.class_id = tcs.class_id AND cu.subject_id = tcs.subject_id
            WHERE NOT EXISTS (
                SELECT 1 FROM teacher_curriculum_items tci
                WHERE tci.teacher_id = tcs.teacher_id AND tci.curriculum_id = cu.id
            );
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}
