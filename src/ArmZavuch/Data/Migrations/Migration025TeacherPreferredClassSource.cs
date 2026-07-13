using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Источник привязки педагога к классу: явное (анкета) или из конструктора.</summary>
public sealed class Migration025TeacherPreferredClassSource : IMigration
{
    public int Version => 25;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            ALTER TABLE teacher_preferred_classes
                ADD COLUMN source TEXT NOT NULL DEFAULT 'explicit'
                    CHECK (source IN ('explicit', 'schedule'));
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}
