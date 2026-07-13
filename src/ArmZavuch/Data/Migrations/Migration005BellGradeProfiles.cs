using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Звонки по параллелям и динамические паузы для начальной школы.</summary>
public sealed class Migration005BellGradeProfiles : IMigration
{
    public int Version => 5;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await AddColumnIfMissingAsync(conn, "bell_templates", "grade_from", "INTEGER NOT NULL DEFAULT 1");
        await AddColumnIfMissingAsync(conn, "bell_templates", "grade_to", "INTEGER NOT NULL DEFAULT 11");
        await AddColumnIfMissingAsync(conn, "bell_periods", "period_kind", "TEXT NOT NULL DEFAULT 'Lesson'");

        await FixBellPeriodsUniqueConstraintAsync(conn);

        await using (var updateStandard = conn.CreateCommand())
        {
            updateStandard.CommandText = """
                UPDATE bell_templates
                SET grade_from = 5, grade_to = 11
                WHERE name LIKE '%Стандарт%' OR name LIKE '%стандарт%'
                """;
            await updateStandard.ExecuteNonQueryAsync();
        }

        await SeedPrimaryTemplatesAsync(conn);
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

    /// <summary>
    /// Старая схема: UNIQUE(template_id, lesson_number, shift) — не допускала перемену
    /// и дин. паузу с тем же номером, что и урок. Расширяем ключ полем period_kind.
    /// </summary>
    private static async Task FixBellPeriodsUniqueConstraintAsync(SqliteConnection conn)
    {
        await using var check = conn.CreateCommand();
        check.CommandText = """
            SELECT sql FROM sqlite_master
            WHERE type = 'table' AND name = 'bell_periods'
            """;
        var ddl = (string?)await check.ExecuteScalarAsync() ?? "";
        if (ddl.Contains("period_kind", StringComparison.Ordinal) &&
            ddl.Contains("UNIQUE(template_id, lesson_number, shift, period_kind)", StringComparison.Ordinal))
            return;

        await using var recreate = conn.CreateCommand();
        recreate.CommandText = """
            CREATE TABLE bell_periods_m5 (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                template_id INTEGER NOT NULL REFERENCES bell_templates(id) ON DELETE CASCADE,
                lesson_number INTEGER NOT NULL,
                shift INTEGER NOT NULL DEFAULT 1,
                start_time TEXT NOT NULL,
                end_time TEXT NOT NULL,
                period_kind TEXT NOT NULL DEFAULT 'Lesson',
                UNIQUE(template_id, lesson_number, shift, period_kind)
            );
            INSERT OR IGNORE INTO bell_periods_m5 (id, template_id, lesson_number, shift, start_time, end_time, period_kind)
                SELECT id, template_id, lesson_number, shift, start_time, end_time, period_kind
                FROM bell_periods;
            DROP TABLE bell_periods;
            ALTER TABLE bell_periods_m5 RENAME TO bell_periods;
            """;
        await recreate.ExecuteNonQueryAsync();
    }

    private static async Task SeedPrimaryTemplatesAsync(SqliteConnection conn)
    {
        await EnsureTemplateWithPeriodsAsync(conn, "1 класс", 1, 1, 1,
        [
            ("Lesson", 1, "08:30", "09:05"),
            ("Break", 0, "09:05", "09:15"),
            ("Lesson", 2, "09:15", "09:50"),
            ("DynamicPause", 2, "09:50", "10:05"),
            ("Lesson", 3, "10:05", "10:40"),
            ("Break", 3, "10:40", "10:55"),
            ("Lesson", 4, "10:55", "11:30")
        ]);

        await EnsureTemplateWithPeriodsAsync(conn, "Начальная (2–4)", 2, 4, 1,
        [
            ("Lesson", 1, "08:30", "09:10"),
            ("Break", 1, "09:10", "09:20"),
            ("Lesson", 2, "09:20", "10:00"),
            ("DynamicPause", 2, "10:00", "10:15"),
            ("Lesson", 3, "10:15", "10:55"),
            ("Break", 3, "10:55", "11:05"),
            ("Lesson", 4, "11:05", "11:45"),
            ("Break", 4, "11:45", "11:55"),
            ("Lesson", 5, "11:55", "12:35")
        ]);
    }

    private static async Task EnsureTemplateWithPeriodsAsync(
        SqliteConnection conn,
        string name,
        int gradeFrom,
        int gradeTo,
        int shift,
        (string Kind, int LessonOrAfter, string Start, string End)[] periods)
    {
        await using var find = conn.CreateCommand();
        find.CommandText = "SELECT id FROM bell_templates WHERE name = $n";
        find.Parameters.AddWithValue("$n", name);
        var existing = await find.ExecuteScalarAsync();
        int templateId;
        if (existing is not null)
        {
            templateId = Convert.ToInt32(existing);
        }
        else
        {
            await using var insert = conn.CreateCommand();
            insert.CommandText = """
                INSERT INTO bell_templates (name, grade_from, grade_to)
                VALUES ($n, $gf, $gt);
                SELECT last_insert_rowid();
                """;
            insert.Parameters.AddWithValue("$n", name);
            insert.Parameters.AddWithValue("$gf", gradeFrom);
            insert.Parameters.AddWithValue("$gt", gradeTo);
            templateId = Convert.ToInt32(await insert.ExecuteScalarAsync());
        }

        foreach (var (kind, lessonOrAfter, start, end) in periods)
        {
            await using var p = conn.CreateCommand();
            p.CommandText = """
                INSERT OR IGNORE INTO bell_periods (template_id, lesson_number, shift, start_time, end_time, period_kind)
                VALUES ($t, $l, $s, $st, $en, $k)
                """;
            p.Parameters.AddWithValue("$t", templateId);
            p.Parameters.AddWithValue("$l", lessonOrAfter);
            p.Parameters.AddWithValue("$s", shift);
            p.Parameters.AddWithValue("$st", start);
            p.Parameters.AddWithValue("$en", end);
            p.Parameters.AddWithValue("$k", kind);
            await p.ExecuteNonQueryAsync();
        }
    }
}
