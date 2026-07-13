using ArmZavuch.Models;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data;

/// <summary>
/// Переименовывает импортированные шаблоны звонков и сводит их к четырём русским именам.
/// Вход: conn. Выход: bell_templates / bell_periods без Legacy и с параллелями 1 / 2–5 / 5–11.
/// </summary>
public static class BellTemplateConsolidation
{
    private sealed record TemplateRow(int Id, string Name, int GradeFrom, int GradeTo);

    private sealed record TemplateStats(
        int TemplateId,
        int Shift,
        string FirstLessonStart,
        string LastLessonEnd,
        int PeriodCount,
        int LessonCount);

    public static async Task ApplyAsync(SqliteConnection conn)
    {
        await StripLegacyPrefixAsync(conn);
        await RenameDisplayAliasesAsync(conn);
        var templates = await LoadTemplatesAsync(conn);
        var stats = await LoadStatsAsync(conn);
        if (templates.Count == 0)
            return;

        var assigned = new HashSet<int>();

        var grade1 = PickTemplate(templates, stats, assigned, t =>
            t.GradeFrom == 1 && t.GradeTo == 1
            || stats.TryGetValue(t.Id, out var s) && s.Shift == 1 && s.LessonCount <= 4 && s.FirstLessonStart == "08:30");
        if (grade1 is not null)
            await PromoteAsync(conn, grade1.Id, BellTemplateNaming.Grade1, 1, 1, assigned);

        var secondShift = PickTemplate(templates, stats, assigned, t =>
            stats.TryGetValue(t.Id, out var s) && s.Shift == 2);
        if (secondShift is not null)
            await PromoteAsync(conn, secondShift.Id, BellTemplateNaming.SecondShift, 5, 11, assigned);

        var standard = PickTemplate(templates, stats, assigned, t =>
            stats.TryGetValue(t.Id, out var s)
            && s.Shift == 1
            && s.FirstLessonStart == "08:30"
            && (s.LessonCount >= 6 || string.CompareOrdinal(s.LastLessonEnd, "14:00") >= 0));
        if (standard is not null)
            await PromoteAsync(conn, standard.Id, BellTemplateNaming.Standard, 5, 11, assigned);

        var primary = PickTemplate(templates, stats, assigned, t =>
            stats.TryGetValue(t.Id, out var s)
            && s.Shift == 1
            && s.FirstLessonStart == "08:30"
            && s.LessonCount is >= 4 and <= 5
            && string.CompareOrdinal(s.LastLessonEnd, "13:30") <= 0);
        if (primary is not null)
            await PromoteAsync(conn, primary.Id, BellTemplateNaming.Primary, 2, 5, assigned);

        foreach (var leftover in templates.Where(t =>
                     !assigned.Contains(t.Id) && IsLegacyImportName(t.Name)))
            await DeleteTemplateAsync(conn, leftover.Id);
    }

    private static bool IsLegacyImportName(string name) =>
        name.StartsWith("1 смена ·", StringComparison.Ordinal)
        || name.StartsWith("2 смена ·", StringComparison.Ordinal)
        || name.Equals(BellTemplateNaming.Grade1, StringComparison.Ordinal)
        || name.Equals(BellTemplateNaming.Primary, StringComparison.Ordinal)
        || name.Equals("Начальная (2–4)", StringComparison.Ordinal)
        || name.Equals(BellTemplateNaming.Standard, StringComparison.Ordinal)
        || name.Equals("Стандарт", StringComparison.Ordinal)
        || name.Contains("Стандарт", StringComparison.OrdinalIgnoreCase);

    private static async Task StripLegacyPrefixAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE bell_templates
            SET name = TRIM(SUBSTR(name, 10))
            WHERE name LIKE 'Legacy · %'
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task RenameDisplayAliasesAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE bell_templates SET name = $new WHERE name = $old;
            """;
        cmd.Parameters.Add("$new", SqliteType.Text);
        cmd.Parameters.Add("$old", SqliteType.Text);

        foreach (var (oldName, newName) in new (string Old, string New)[]
                 {
                     ("Начальная (2–4)", BellTemplateNaming.Primary),
                     ("Стандарт", BellTemplateNaming.Standard)
                 })
        {
            cmd.Parameters["$new"].Value = newName;
            cmd.Parameters["$old"].Value = oldName;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static TemplateRow? PickTemplate(
        IReadOnlyList<TemplateRow> templates,
        IReadOnlyDictionary<int, TemplateStats> stats,
        IReadOnlySet<int> assigned,
        Func<TemplateRow, bool> predicate)
    {
        return templates
            .Where(t => !assigned.Contains(t.Id) && predicate(t))
            .OrderByDescending(t => stats.TryGetValue(t.Id, out var s) ? s.PeriodCount : 0)
            .ThenBy(t => t.Id)
            .FirstOrDefault();
    }

    private static async Task PromoteAsync(
        SqliteConnection conn,
        int templateId,
        string canonicalName,
        int gradeFrom,
        int gradeTo,
        HashSet<int> assigned)
    {
        var targetId = await EnsureCanonicalTemplateAsync(conn, canonicalName, gradeFrom, gradeTo);
        if (targetId != templateId)
            await MergeTemplateAsync(conn, targetId, templateId);

        assigned.Add(templateId);
        assigned.Add(targetId);
    }

    private static async Task<int> EnsureCanonicalTemplateAsync(
        SqliteConnection conn,
        string name,
        int gradeFrom,
        int gradeTo)
    {
        await using (var find = conn.CreateCommand())
        {
            find.CommandText = "SELECT id FROM bell_templates WHERE name = $n";
            find.Parameters.AddWithValue("$n", name);
            var existing = await find.ExecuteScalarAsync();
            if (existing is not null)
            {
                var id = Convert.ToInt32(existing);
                await using var upd = conn.CreateCommand();
                upd.CommandText = "UPDATE bell_templates SET grade_from = $f, grade_to = $t WHERE id = $id";
                upd.Parameters.AddWithValue("$f", gradeFrom);
                upd.Parameters.AddWithValue("$t", gradeTo);
                upd.Parameters.AddWithValue("$id", id);
                await upd.ExecuteNonQueryAsync();
                return id;
            }
        }

        await using var ins = conn.CreateCommand();
        ins.CommandText = """
            INSERT INTO bell_templates (name, grade_from, grade_to)
            VALUES ($n, $f, $t);
            SELECT last_insert_rowid();
            """;
        ins.Parameters.AddWithValue("$n", name);
        ins.Parameters.AddWithValue("$f", gradeFrom);
        ins.Parameters.AddWithValue("$t", gradeTo);
        return Convert.ToInt32(await ins.ExecuteScalarAsync());
    }

    private static async Task MergeTemplateAsync(SqliteConnection conn, int targetId, int sourceId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM bell_periods
            WHERE template_id = $source
              AND EXISTS (
                SELECT 1 FROM bell_periods t
                WHERE t.template_id = $target
                  AND t.lesson_number = bell_periods.lesson_number
                  AND t.shift = bell_periods.shift
                  AND t.period_kind = bell_periods.period_kind
              );
            UPDATE bell_periods SET template_id = $target WHERE template_id = $source;
            DELETE FROM bell_templates WHERE id = $source;
            """;
        cmd.Parameters.AddWithValue("$target", targetId);
        cmd.Parameters.AddWithValue("$source", sourceId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task DeleteTemplateAsync(SqliteConnection conn, int templateId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM bell_templates WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", templateId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<List<TemplateRow>> LoadTemplatesAsync(SqliteConnection conn)
    {
        var list = new List<TemplateRow>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, grade_from, grade_to FROM bell_templates ORDER BY id";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new TemplateRow(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetInt32(3)));
        }

        return list;
    }

    private static async Task<Dictionary<int, TemplateStats>> LoadStatsAsync(SqliteConnection conn)
    {
        var map = new Dictionary<int, TemplateStats>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT template_id, shift, period_kind, lesson_number, start_time, end_time
            FROM bell_periods
            ORDER BY template_id, shift, start_time
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var templateId = reader.GetInt32(0);
            var shift = reader.GetInt32(1);
            var kind = reader.GetString(2);
            var start = reader.GetString(4);
            var end = reader.GetString(5);

            if (!map.TryGetValue(templateId, out var stats))
            {
                stats = new TemplateStats(templateId, shift, "", "", 0, 0);
                map[templateId] = stats;
            }

            if (BellPeriodKinds.IsLesson(kind))
            {
                stats = stats with
                {
                    Shift = shift,
                    PeriodCount = stats.PeriodCount + 1,
                    LessonCount = stats.LessonCount + 1,
                    FirstLessonStart = stats.FirstLessonStart.Length == 0
                        || string.CompareOrdinal(start, stats.FirstLessonStart) < 0
                        ? start
                        : stats.FirstLessonStart,
                    LastLessonEnd = stats.LastLessonEnd.Length == 0
                        || string.CompareOrdinal(end, stats.LastLessonEnd) > 0
                        ? end
                        : stats.LastLessonEnd
                };
            }
            else
            {
                stats = stats with
                {
                    Shift = shift,
                    PeriodCount = stats.PeriodCount + 1
                };
            }
            map[templateId] = stats;
        }

        return map;
    }
}
