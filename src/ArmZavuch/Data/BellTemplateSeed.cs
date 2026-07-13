using ArmZavuch.Models;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data;

/// <summary>Восстанавливает встроенные шаблоны звонков при пустой или очищенной базе.</summary>
public static class BellTemplateSeed
{
    public static async Task EnsureBuiltInAsync(SqliteConnection conn)
    {
        await EnsureTemplateWithPeriodsAsync(conn, BellTemplateNaming.Grade1, 1, 1, 1,
        [
            ("Lesson", 1, "08:30", "09:05"),
            ("Break", 1, "09:05", "09:15"),
            ("Lesson", 2, "09:15", "09:50"),
            ("Break", 2, "09:50", "10:10"),
            ("Lesson", 3, "10:10", "10:45"),
            ("DynamicPause", 3, "10:45", "11:25"),
            ("Lesson", 4, "11:25", "12:00"),
            ("Break", 4, "12:00", "12:10"),
            ("Lesson", 5, "12:10", "12:45")
        ]);

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

        await EnsureTemplateWithPeriodsAsync(conn, BellTemplateNaming.Primary, 2, 5, 1,
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

        await EnsureTemplateWithPeriodsAsync(conn, BellTemplateNaming.Standard, 5, 11, 1,
        [
            ("Lesson", 1, "08:30", "09:10"),
            ("Break", 1, "09:10", "09:25"),
            ("Lesson", 2, "09:25", "10:05"),
            ("Break", 2, "10:05", "10:20"),
            ("Lesson", 3, "10:20", "11:00"),
            ("Break", 3, "11:00", "11:10"),
            ("Lesson", 4, "11:10", "11:50"),
            ("Break", 4, "11:50", "12:00"),
            ("Lesson", 5, "12:00", "12:40"),
            ("Break", 5, "12:40", "12:55"),
            ("Lesson", 6, "12:55", "13:35"),
            ("Break", 6, "13:35", "13:50"),
            ("Lesson", 7, "13:50", "14:30"),
            ("Break", 7, "14:30", "14:55"),
            ("Lesson", 8, "14:55", "15:30")
        ]);

        await EnsureTemplateWithPeriodsAsync(conn, BellTemplateNaming.SecondShift, 5, 11, 2,
        [
            ("Lesson", 1, "11:10", "11:50"),
            ("Break", 1, "11:50", "12:00"),
            ("Lesson", 2, "12:00", "12:40"),
            ("Break", 2, "12:40", "12:55"),
            ("Lesson", 3, "12:55", "13:35"),
            ("Break", 3, "13:35", "13:50"),
            ("Lesson", 4, "13:50", "14:30"),
            ("Break", 4, "14:30", "14:40"),
            ("Lesson", 5, "14:40", "15:20"),
            ("Break", 5, "15:20", "15:30"),
            ("Lesson", 6, "15:30", "16:10"),
            ("Break", 6, "16:10", "16:25"),
            ("Lesson", 7, "16:25", "17:05")
        ]);

        await SeedDefaultAssignmentsAsync(conn);
    }

    private static async Task SeedDefaultAssignmentsAsync(SqliteConnection conn)
    {
        foreach (var (key, value) in new (string Key, string Value)[]
                 {
                     ("bell.default.grade1", BellTemplateNaming.Grade1),
                     ("bell.default.grade1.secondHalf", BellTemplateNaming.Grade1SecondHalf),
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
            await using var update = conn.CreateCommand();
            update.CommandText = "UPDATE bell_templates SET grade_from = $f, grade_to = $t WHERE id = $id";
            update.Parameters.AddWithValue("$f", gradeFrom);
            update.Parameters.AddWithValue("$t", gradeTo);
            update.Parameters.AddWithValue("$id", templateId);
            await update.ExecuteNonQueryAsync();
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

        await using var count = conn.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM bell_periods WHERE template_id = $t";
        count.Parameters.AddWithValue("$t", templateId);
        if (Convert.ToInt32(await count.ExecuteScalarAsync()) > 0)
            return;

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
