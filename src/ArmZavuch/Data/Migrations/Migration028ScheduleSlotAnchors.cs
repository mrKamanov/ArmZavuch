using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Флаг закрепления слота: авторасписание не изменяет якорные ячейки.</summary>
public sealed class Migration028ScheduleSlotAnchors : IMigration
{
    public int Version => 28;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            ALTER TABLE week_template_slots
            ADD COLUMN is_anchored INTEGER NOT NULL DEFAULT 0;
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}
