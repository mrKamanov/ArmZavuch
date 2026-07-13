using ArmZavuch.Models;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Шаблон звонков по классу и значения по умолчанию для 1 / 2–11 классов по сменам.</summary>
public sealed class Migration023BellTemplateAssignments : IMigration
{
    public int Version => 23;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await AddColumnIfMissingAsync(conn, "school_classes", "bell_template_id",
            "INTEGER REFERENCES bell_templates(id)");

        await SeedDefaultsAsync(conn);
    }

    private static async Task SeedDefaultsAsync(SqliteConnection conn)
    {
        foreach (var (key, value) in new (string Key, string Value)[]
                 {
                     ("bell.default.grade1", BellTemplateNaming.Grade1),
                     ("bell.default.shift1", BellTemplateNaming.Standard),
                     ("bell.default.shift2", BellTemplateNaming.SecondShift)
                 })
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO app_settings (key, value) VALUES ($k, $v)
                ON CONFLICT(key) DO NOTHING
                """;
            cmd.Parameters.AddWithValue("$k", key);
            cmd.Parameters.AddWithValue("$v", value);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task AddColumnIfMissingAsync(SqliteConnection conn, string table, string column, string ddl)
    {
        await using var check = conn.CreateCommand();
        check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = $n";
        check.Parameters.AddWithValue("$n", column);
        if (Convert.ToInt32(await check.ExecuteScalarAsync()) > 0)
            return;

        await using var alter = conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {ddl}";
        await alter.ExecuteNonQueryAsync();
    }
}
