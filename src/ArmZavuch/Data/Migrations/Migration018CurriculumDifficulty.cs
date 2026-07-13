using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Балл Сивкова на строке нагрузки (per class+subject), не в справочнике предметов.</summary>
public sealed class Migration018CurriculumDifficulty : IMigration
{
    public int Version => 18;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await AddColumnIfMissingAsync(conn, "curriculum", "difficulty_score", "REAL");

        await using var backfill = conn.CreateCommand();
        backfill.CommandText = """
            UPDATE curriculum
            SET difficulty_score = (
                SELECT s.difficulty_score FROM subjects s WHERE s.id = curriculum.subject_id
            )
            WHERE difficulty_score IS NULL;
            """;
        await backfill.ExecuteNonQueryAsync();
    }

    private static async Task AddColumnIfMissingAsync(SqliteConnection conn, string table, string column, string definition)
    {
        await using var check = conn.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table})";
        await using var reader = await check.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader.GetString(1).Equals(column, StringComparison.OrdinalIgnoreCase))
                return;
        }

        await using var alter = conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
        await alter.ExecuteNonQueryAsync();
    }
}
