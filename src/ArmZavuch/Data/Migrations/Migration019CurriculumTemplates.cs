using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Шаблоны нагрузки и встроенные наборы для 1 и 2–4 классов.</summary>
public sealed class Migration019CurriculumTemplates : IMigration
{
    public int Version => 19;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await using (var create = conn.CreateCommand())
        {
            create.CommandText = """
                CREATE TABLE IF NOT EXISTS curriculum_templates (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL UNIQUE,
                    grade_from INTEGER NOT NULL,
                    grade_to INTEGER NOT NULL,
                    is_builtin INTEGER NOT NULL DEFAULT 0,
                    sort_order INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS curriculum_template_items (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    template_id INTEGER NOT NULL REFERENCES curriculum_templates(id) ON DELETE CASCADE,
                    subject_name TEXT NOT NULL,
                    hours_per_week REAL NOT NULL,
                    difficulty_score REAL NOT NULL,
                    has_subgroups INTEGER NOT NULL DEFAULT 0,
                    week_parity TEXT NOT NULL DEFAULT 'EveryWeek',
                    item_grade_from INTEGER NOT NULL DEFAULT 0,
                    item_grade_to INTEGER NOT NULL DEFAULT 0
                );
                """;
            await create.ExecuteNonQueryAsync();
        }

        await CurriculumTemplateSeed.EnsureBuiltInAsync(conn);
    }
}
