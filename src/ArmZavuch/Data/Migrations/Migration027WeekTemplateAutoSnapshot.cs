using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Таблица снимков недельного шаблона перед авторасписанием (откат).</summary>
public sealed class Migration027WeekTemplateAutoSnapshot : IMigration
{
    public int Version => 27;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS week_template_auto_snapshots (
                week_template_id INTEGER PRIMARY KEY,
                snapshot_json TEXT NOT NULL,
                FOREIGN KEY (week_template_id) REFERENCES week_templates(id) ON DELETE CASCADE
            );
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}
