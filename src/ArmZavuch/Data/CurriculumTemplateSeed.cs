using System.Text.Json;
using ArmZavuch.Models;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data;

/// <summary>
/// Загружает и обновляет встроенные шаблоны нагрузки из embedded JSON.
/// Вход: conn. Выход: строки в curriculum_templates / curriculum_template_items, subjects.difficulty_score.
/// </summary>
public static class CurriculumTemplateSeed
{
    private static readonly string[] BuiltInFiles =
    [
        "grade1.json",
        "grade2.json",
        "grade3.json",
        "grade4.json",
        "grade5.json",
        "grade6.json",
        "grade7.json",
        "grade8.json",
        "grade9.json"
    ];

    public static async Task EnsureBuiltInAsync(SqliteConnection conn)
    {
        await RemoveObsoleteCombinedTemplateAsync(conn);

        var order = 0;
        foreach (var file in BuiltInFiles)
        {
            var dto = await ReadTemplateDtoAsync(file);
            if (dto is null)
                continue;

            await UpsertBuiltInTemplateAsync(conn, dto, order++);
        }
    }

    /// <summary>Перезаписывает встроенные шаблоны из JSON и обновляет difficulty_score в subjects.</summary>
    public static async Task RefreshBuiltInAsync(SqliteConnection conn)
    {
        await EnsureBuiltInAsync(conn);
        await RefreshSubjectDefaultsFromBuiltInAsync(conn);
    }

    private static async Task UpsertBuiltInTemplateAsync(SqliteConnection conn, CurriculumTemplateFileDto dto, int sortOrder)
    {
        int? templateId = null;
        await using (var find = conn.CreateCommand())
        {
            find.CommandText = "SELECT id FROM curriculum_templates WHERE is_builtin = 1 AND name = $n";
            find.Parameters.AddWithValue("$n", dto.Name);
            var existing = await find.ExecuteScalarAsync();
            if (existing is not null)
                templateId = Convert.ToInt32(existing);
        }

        if (templateId is null)
        {
            await InsertTemplateAsync(conn, dto, sortOrder);
            return;
        }

        await using (var upd = conn.CreateCommand())
        {
            upd.CommandText = """
                UPDATE curriculum_templates
                SET grade_from = $gf, grade_to = $gt, sort_order = $so
                WHERE id = $id
                """;
            upd.Parameters.AddWithValue("$gf", dto.GradeFrom);
            upd.Parameters.AddWithValue("$gt", dto.GradeTo);
            upd.Parameters.AddWithValue("$so", sortOrder);
            upd.Parameters.AddWithValue("$id", templateId.Value);
            await upd.ExecuteNonQueryAsync();
        }

        await using (var del = conn.CreateCommand())
        {
            del.CommandText = "DELETE FROM curriculum_template_items WHERE template_id = $id";
            del.Parameters.AddWithValue("$id", templateId.Value);
            await del.ExecuteNonQueryAsync();
        }

        await InsertTemplateItemsAsync(conn, templateId.Value, dto.Items);
    }

    private static async Task RefreshSubjectDefaultsFromBuiltInAsync(SqliteConnection conn)
    {
        var scores = new Dictionary<string, (int Grade, double Score)>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in BuiltInFiles)
        {
            var dto = await ReadTemplateDtoAsync(file);
            if (dto is null)
                continue;

            foreach (var item in dto.Items)
            {
                if (!scores.TryGetValue(item.SubjectName, out var prev) || dto.GradeFrom > prev.Grade)
                    scores[item.SubjectName] = (dto.GradeFrom, item.DifficultyScore);
            }
        }

        foreach (var (name, (_, score)) in scores)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE subjects SET difficulty_score = $d WHERE name = $n COLLATE NOCASE";
            cmd.Parameters.AddWithValue("$d", score);
            cmd.Parameters.AddWithValue("$n", name);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task<CurriculumTemplateFileDto?> ReadTemplateDtoAsync(string fileName)
    {
        var json = await ReadTemplateJsonAsync(fileName);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        var dto = JsonSerializer.Deserialize<CurriculumTemplateFileDto>(json);
        return dto is null || dto.Items.Count == 0 ? null : dto;
    }

    private static async Task RemoveObsoleteCombinedTemplateAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM curriculum_template_items WHERE template_id IN (
                SELECT id FROM curriculum_templates
                WHERE is_builtin = 1 AND grade_from = 2 AND grade_to = 4
            );
            DELETE FROM curriculum_templates
            WHERE is_builtin = 1 AND grade_from = 2 AND grade_to = 4;
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ReadTemplateJsonAsync(string fileName)
    {
        var assembly = typeof(CurriculumTemplateSeed).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (resourceName is not null)
        {
            await using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        var path = Path.Combine(AppContext.BaseDirectory, "Data", "CurriculumTemplates", fileName);
        return File.Exists(path) ? await File.ReadAllTextAsync(path) : null;
    }

    private static async Task InsertTemplateAsync(SqliteConnection conn, CurriculumTemplateFileDto dto, int sortOrder)
    {
        await using var ins = conn.CreateCommand();
        ins.CommandText = """
            INSERT INTO curriculum_templates (name, grade_from, grade_to, is_builtin, sort_order)
            VALUES ($n, $gf, $gt, 1, $so); SELECT last_insert_rowid();
            """;
        ins.Parameters.AddWithValue("$n", dto.Name);
        ins.Parameters.AddWithValue("$gf", dto.GradeFrom);
        ins.Parameters.AddWithValue("$gt", dto.GradeTo);
        ins.Parameters.AddWithValue("$so", sortOrder);
        var templateId = Convert.ToInt32(await ins.ExecuteScalarAsync());
        await InsertTemplateItemsAsync(conn, templateId, dto.Items);
    }

    private static async Task InsertTemplateItemsAsync(
        SqliteConnection conn,
        int templateId,
        IReadOnlyList<CurriculumTemplateFileItemDto> items)
    {
        foreach (var item in items)
        {
            await using var row = conn.CreateCommand();
            row.CommandText = """
                INSERT INTO curriculum_template_items
                    (template_id, subject_name, hours_per_week, difficulty_score, has_subgroups, week_parity, item_grade_from, item_grade_to)
                VALUES ($t, $s, $h, $d, $g, $p, $gf, $gt)
                """;
            row.Parameters.AddWithValue("$t", templateId);
            row.Parameters.AddWithValue("$s", item.SubjectName);
            row.Parameters.AddWithValue("$h", item.HoursPerWeek);
            row.Parameters.AddWithValue("$d", item.DifficultyScore);
            row.Parameters.AddWithValue("$g", item.HasSubgroups ? 1 : 0);
            row.Parameters.AddWithValue("$p", item.WeekParity);
            row.Parameters.AddWithValue("$gf", item.GradeFrom);
            row.Parameters.AddWithValue("$gt", item.GradeTo);
            await row.ExecuteNonQueryAsync();
        }
    }
}
