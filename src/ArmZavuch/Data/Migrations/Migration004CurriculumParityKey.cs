using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Один класс+предмет может быть в нагрузке для разных недель (А/Б).</summary>
public sealed class Migration004CurriculumParityKey : IMigration
{
    public int Version => 4;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await using var tx = await conn.BeginTransactionAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = (SqliteTransaction)tx;
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS curriculum_new (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    class_id INTEGER NOT NULL REFERENCES school_classes(id),
                    subject_id INTEGER NOT NULL REFERENCES subjects(id),
                    hours_per_week REAL NOT NULL,
                    has_subgroups INTEGER NOT NULL DEFAULT 0,
                    week_parity TEXT NOT NULL DEFAULT 'EveryWeek',
                    UNIQUE(class_id, subject_id, week_parity)
                );
                INSERT INTO curriculum_new (id, class_id, subject_id, hours_per_week, has_subgroups, week_parity)
                SELECT id, class_id, subject_id, hours_per_week, has_subgroups,
                       COALESCE(week_parity, 'EveryWeek')
                FROM curriculum;
                DROP TABLE curriculum;
                ALTER TABLE curriculum_new RENAME TO curriculum;
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
    }
}
