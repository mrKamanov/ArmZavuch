using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Источник назначения нагрузки: явное (анкета) или из конструктора.</summary>
public sealed class Migration024TeacherCurriculumSource : IMigration
{
    public int Version => 24;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            ALTER TABLE teacher_curriculum_items
                ADD COLUMN source TEXT NOT NULL DEFAULT 'explicit'
                    CHECK (source IN ('explicit', 'schedule'));
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}
