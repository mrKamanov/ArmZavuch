using ArmZavuch.Models;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Шаблон звонков 1 класса для II полугодия и назначение по умолчанию.</summary>
public sealed class Migration033BellGrade1SecondHalf : IMigration
{
    public int Version => 33;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await EnsureTemplateAsync(conn);
        await SeedDefaultAssignmentAsync(conn);
    }

    private static async Task EnsureTemplateAsync(SqliteConnection conn)
    {
        await EnsureTemplateWithPeriodsAsync(conn, BellTemplateNaming.Grade1SecondHalf, 1, 1, 1,
        [
            ("Lesson", 1, "08:30", "09:10"),
            ("Break", 1, "09:10", "09:25"),
            ("Lesson", 2, "09:25", "10:05"),
            ("Break", 2, "10:05", "10:15"),
            ("Lesson", 3, "10:15", "10:55"),
            ("DynamicPause", 3, "10:55", "11:35"),
            ("Lesson", 4, "11:35", "12:15"),
            ("Break", 4, "12:15", "12:25"),
            ("Lesson", 5, "12:25", "13:05")
        ]);
    }

    private static async Task SeedDefaultAssignmentAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO app_settings (key, value) VALUES ($k, $v)
            ON CONFLICT(key) DO NOTHING
            """;
        cmd.Parameters.AddWithValue("$k", "bell.default.grade1.secondHalf");
        cmd.Parameters.AddWithValue("$v", BellTemplateNaming.Grade1SecondHalf);
        await cmd.ExecuteNonQueryAsync();
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
            await using var period = conn.CreateCommand();
            period.CommandText = """
                INSERT OR IGNORE INTO bell_periods (template_id, lesson_number, shift, start_time, end_time, period_kind)
                VALUES ($t, $l, $s, $st, $en, $k)
                """;
            period.Parameters.AddWithValue("$t", templateId);
            period.Parameters.AddWithValue("$l", lessonOrAfter);
            period.Parameters.AddWithValue("$s", shift);
            period.Parameters.AddWithValue("$st", start);
            period.Parameters.AddWithValue("$en", end);
            period.Parameters.AddWithValue("$k", kind);
            await period.ExecuteNonQueryAsync();
        }
    }
}
