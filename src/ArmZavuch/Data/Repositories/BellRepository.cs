using ArmZavuch.Data;
using ArmZavuch.Models;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Repositories;

/// <summary>Шаблоны звонков и периоды уроков.</summary>
public sealed class BellRepository
{
    private readonly SqliteConnectionFactory _factory;

    public BellRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<List<BellPeriod>> GetAllPeriodsAsync()
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT p.id, p.template_id, t.name, t.grade_from, t.grade_to,
                   p.lesson_number, p.shift, p.start_time, p.end_time, p.period_kind
            FROM bell_periods p
            JOIN bell_templates t ON t.id = p.template_id
            ORDER BY t.grade_from, t.name, p.shift, p.period_kind, p.lesson_number, p.start_time
            """;
        return await ReadListAsync(cmd);
    }

    public async Task<int> EnsureTemplateAsync(string name, int gradeFrom = 1, int gradeTo = 11)
    {
        await using var conn = _factory.CreateConnection();
        await using var find = conn.CreateCommand();
        find.CommandText = "SELECT id FROM bell_templates WHERE name = $n";
        find.Parameters.AddWithValue("$n", name);
        var existing = await find.ExecuteScalarAsync();
        if (existing is not null)
            return Convert.ToInt32(existing);

        await using var insert = conn.CreateCommand();
        insert.CommandText = """
            INSERT INTO bell_templates (name, grade_from, grade_to)
            VALUES ($n, $gf, $gt);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("$n", name);
        insert.Parameters.AddWithValue("$gf", gradeFrom);
        insert.Parameters.AddWithValue("$gt", gradeTo);
        return Convert.ToInt32(await insert.ExecuteScalarAsync());
    }

    public async Task UpdateTemplateGradesAsync(int templateId, int gradeFrom, int gradeTo)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE bell_templates SET grade_from = $f, grade_to = $t WHERE id = $id";
        cmd.Parameters.AddWithValue("$f", gradeFrom);
        cmd.Parameters.AddWithValue("$t", gradeTo);
        cmd.Parameters.AddWithValue("$id", templateId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int?> FindTemplateIdByNameAsync(string name)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM bell_templates WHERE name = $n";
        cmd.Parameters.AddWithValue("$n", name);
        var result = await cmd.ExecuteScalarAsync();
        return result is null ? null : Convert.ToInt32(result);
    }

    public async Task ConsolidateTemplatesAsync()
    {
        await using var conn = _factory.CreateConnection();
        await BellTemplateConsolidation.ApplyAsync(conn);
    }

    public async Task<List<string>> GetTemplateNamesAsync()
    {
        await using var conn = _factory.CreateConnection();
        await DeleteOrphanTemplatesAsync(conn);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT DISTINCT t.name
            FROM bell_templates t
            WHERE EXISTS (SELECT 1 FROM bell_periods p WHERE p.template_id = t.id)
               OR EXISTS (SELECT 1 FROM school_classes c WHERE c.bell_template_id = t.id)
               OR EXISTS (SELECT 1 FROM day_overrides d WHERE d.bell_template_id = t.id)
            ORDER BY t.grade_from, t.name
            """;
        var list = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(reader.GetString(0));
        return list;
    }

    public async Task<List<BellTemplateRow>> GetAllTemplatesAsync()
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, name, grade_from, grade_to
            FROM bell_templates
            ORDER BY grade_from, name
            """;
        var list = new List<BellTemplateRow>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new BellTemplateRow(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetInt32(3)));
        }

        return list;
    }

    public async Task<int> CountClassesUsingTemplateAsync(int templateId)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM school_classes WHERE bell_template_id = $id";
        cmd.Parameters.AddWithValue("$id", templateId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task RenameTemplateAsync(int templateId, string newName)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE bell_templates SET name = $n WHERE id = $id";
        cmd.Parameters.AddWithValue("$n", newName.Trim());
        cmd.Parameters.AddWithValue("$id", templateId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateTemplateMetaAsync(int templateId, string name, int gradeFrom, int gradeTo)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE bell_templates
            SET name = $n, grade_from = $f, grade_to = $t
            WHERE id = $id
            """;
        cmd.Parameters.AddWithValue("$n", name.Trim());
        cmd.Parameters.AddWithValue("$f", gradeFrom);
        cmd.Parameters.AddWithValue("$t", gradeTo);
        cmd.Parameters.AddWithValue("$id", templateId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> DuplicateTemplateAsync(int sourceTemplateId, string newName, int gradeFrom, int gradeTo)
    {
        var newId = await EnsureTemplateAsync(newName, gradeFrom, gradeTo);
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO bell_periods (template_id, lesson_number, shift, start_time, end_time, period_kind)
            SELECT $newId, lesson_number, shift, start_time, end_time, period_kind
            FROM bell_periods
            WHERE template_id = $sourceId
            """;
        cmd.Parameters.AddWithValue("$newId", newId);
        cmd.Parameters.AddWithValue("$sourceId", sourceTemplateId);
        await cmd.ExecuteNonQueryAsync();
        return newId;
    }

    public async Task DeleteTemplateAsync(int templateId)
    {
        await using var conn = _factory.CreateConnection();
        await using var deletePeriods = conn.CreateCommand();
        deletePeriods.CommandText = "DELETE FROM bell_periods WHERE template_id = $id";
        deletePeriods.Parameters.AddWithValue("$id", templateId);
        await deletePeriods.ExecuteNonQueryAsync();

        await using var deleteTemplate = conn.CreateCommand();
        deleteTemplate.CommandText = "DELETE FROM bell_templates WHERE id = $id";
        deleteTemplate.Parameters.AddWithValue("$id", templateId);
        await deleteTemplate.ExecuteNonQueryAsync();
    }

    public async Task<int> InsertPeriodAsync(BellPeriod period)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO bell_periods (template_id, lesson_number, shift, start_time, end_time, period_kind)
            VALUES ($t, $l, $s, $st, $en, $k);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$t", period.TemplateId);
        cmd.Parameters.AddWithValue("$l", period.LessonNumber);
        cmd.Parameters.AddWithValue("$s", period.Shift);
        cmd.Parameters.AddWithValue("$st", period.StartTime);
        cmd.Parameters.AddWithValue("$en", period.EndTime);
        cmd.Parameters.AddWithValue("$k", period.PeriodKind);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task UpdateAsync(BellPeriod period)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE bell_periods
            SET template_id = $t, lesson_number = $l, shift = $s, start_time = $st, end_time = $en, period_kind = $k
            WHERE id = $id
            """;
        cmd.Parameters.AddWithValue("$t", period.TemplateId);
        cmd.Parameters.AddWithValue("$l", period.LessonNumber);
        cmd.Parameters.AddWithValue("$s", period.Shift);
        cmd.Parameters.AddWithValue("$st", period.StartTime);
        cmd.Parameters.AddWithValue("$en", period.EndTime);
        cmd.Parameters.AddWithValue("$k", period.PeriodKind);
        cmd.Parameters.AddWithValue("$id", period.Id);
        await cmd.ExecuteNonQueryAsync();
        await UpdateTemplateGradesAsync(period.TemplateId, period.TemplateGradeFrom, period.TemplateGradeTo);
    }

    public async Task DeleteAsync(int id)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM bell_periods WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
        await DeleteOrphanTemplatesAsync(conn);
    }

    /// <summary>Удаляет шаблоны без строк звонков и без ссылок из классов и суточных override.</summary>
    public async Task DeleteOrphanTemplatesAsync()
    {
        await using var conn = _factory.CreateConnection();
        await DeleteOrphanTemplatesAsync(conn);
    }

    private static async Task DeleteOrphanTemplatesAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM bell_templates
            WHERE id NOT IN (SELECT DISTINCT template_id FROM bell_periods)
              AND id NOT IN (
                  SELECT bell_template_id FROM school_classes
                  WHERE bell_template_id IS NOT NULL
              )
              AND id NOT IN (
                  SELECT bell_template_id FROM day_overrides
                  WHERE bell_template_id IS NOT NULL
              );
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<List<BellPeriod>> ReadListAsync(SqliteCommand cmd)
    {
        var list = new List<BellPeriod>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new BellPeriod
            {
                Id = reader.GetInt32(0),
                TemplateId = reader.GetInt32(1),
                TemplateName = reader.GetString(2),
                TemplateGradeFrom = reader.GetInt32(3),
                TemplateGradeTo = reader.GetInt32(4),
                LessonNumber = reader.GetInt32(5),
                Shift = reader.GetInt32(6),
                StartTime = reader.GetString(7),
                EndTime = reader.GetString(8),
                PeriodKind = reader.GetString(9)
            });
        }
        return list;
    }
}
