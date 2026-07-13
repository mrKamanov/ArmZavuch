using ArmZavuch.Models;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Repositories;

/// <summary>CRUD шаблонов нагрузки: встроенные (read-only) и пользовательские.</summary>
public sealed class CurriculumTemplateRepository
{
    private readonly SqliteConnectionFactory _factory;

    public CurriculumTemplateRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<List<CurriculumTemplate>> GetAllAsync()
    {
        await using var conn = _factory.CreateConnection();
        await CurriculumTemplateSeed.EnsureBuiltInAsync(conn);

        var templates = new List<CurriculumTemplate>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT id, name, grade_from, grade_to, is_builtin, sort_order
                FROM curriculum_templates
                ORDER BY sort_order, grade_from, name
                """;
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                templates.Add(new CurriculumTemplate
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    GradeFrom = reader.GetInt32(2),
                    GradeTo = reader.GetInt32(3),
                    IsBuiltIn = reader.GetInt32(4) == 1,
                    SortOrder = reader.GetInt32(5)
                });
            }
        }

        if (templates.Count == 0)
            return templates;

        await using (var itemsCmd = conn.CreateCommand())
        {
            itemsCmd.CommandText = """
                SELECT template_id, id, subject_name, hours_per_week, difficulty_score,
                       has_subgroups, week_parity, item_grade_from, item_grade_to
                FROM curriculum_template_items
                ORDER BY template_id, id
                """;
            var byId = templates.ToDictionary(t => t.Id);
            await using var reader = await itemsCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var templateId = reader.GetInt32(0);
                if (!byId.TryGetValue(templateId, out var template))
                    continue;
                template.Items.Add(new CurriculumTemplateItem
                {
                    TemplateId = templateId,
                    Id = reader.GetInt32(1),
                    SubjectName = reader.GetString(2),
                    HoursPerWeek = reader.GetDouble(3),
                    DifficultyScore = reader.GetDouble(4),
                    HasSubgroups = reader.GetInt32(5) == 1,
                    WeekParity = reader.GetString(6),
                    ItemGradeFrom = reader.GetInt32(7),
                    ItemGradeTo = reader.GetInt32(8)
                });
            }
        }

        return templates;
    }

    public async Task<bool> NameExistsAsync(string name, int? excludeId = null)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = excludeId is int id
            ? "SELECT 1 FROM curriculum_templates WHERE name = $n COLLATE NOCASE AND id <> $id LIMIT 1"
            : "SELECT 1 FROM curriculum_templates WHERE name = $n COLLATE NOCASE LIMIT 1";
        cmd.Parameters.AddWithValue("$n", name.Trim());
        if (excludeId is int excluded)
            cmd.Parameters.AddWithValue("$id", excluded);
        return await cmd.ExecuteScalarAsync() is not null;
    }

    public async Task<int> CreateUserTemplateAsync(string name, int grade, IReadOnlyList<CurriculumTemplateItem> items)
    {
        await using var conn = _factory.CreateConnection();
        await using var tx = await conn.BeginTransactionAsync();
        var sortOrder = await GetNextSortOrderAsync(conn);
        var templateId = await InsertHeaderAsync(conn, name, grade, grade, isBuiltIn: false, sortOrder);
        await ReplaceItemsAsync(conn, templateId, items);
        await tx.CommitAsync();
        return templateId;
    }

    public async Task<int> CopyAsUserTemplateAsync(int sourceId, string newName)
    {
        var source = (await GetAllAsync()).FirstOrDefault(t => t.Id == sourceId)
            ?? throw new InvalidOperationException("Шаблон не найден");
        var items = source.Items.Select(CloneItem).ToList();
        return await CreateUserTemplateAsync(newName, source.GradeFrom, items);
    }

    public async Task UpdateUserTemplateAsync(
        int id,
        string name,
        int grade,
        IReadOnlyList<CurriculumTemplateItem> items)
    {
        await using var conn = _factory.CreateConnection();
        await EnsureUserTemplateAsync(conn, id);
        await using var tx = await conn.BeginTransactionAsync();
        await using (var upd = conn.CreateCommand())
        {
            upd.CommandText = """
                UPDATE curriculum_templates
                SET name = $n, grade_from = $gf, grade_to = $gt
                WHERE id = $id AND is_builtin = 0
                """;
            upd.Parameters.AddWithValue("$n", name.Trim());
            upd.Parameters.AddWithValue("$gf", grade);
            upd.Parameters.AddWithValue("$gt", grade);
            upd.Parameters.AddWithValue("$id", id);
            if (await upd.ExecuteNonQueryAsync() == 0)
                throw new InvalidOperationException("Встроенный шаблон нельзя изменить");
        }

        await ReplaceItemsAsync(conn, id, items);
        await tx.CommitAsync();
    }

    public async Task DeleteUserTemplateAsync(int id)
    {
        await using var conn = _factory.CreateConnection();
        await EnsureUserTemplateAsync(conn, id);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM curriculum_templates WHERE id = $id AND is_builtin = 0";
        cmd.Parameters.AddWithValue("$id", id);
        if (await cmd.ExecuteNonQueryAsync() == 0)
            throw new InvalidOperationException("Встроенный шаблон нельзя удалить");
    }

    private static CurriculumTemplateItem CloneItem(CurriculumTemplateItem item) => new()
    {
        SubjectName = item.SubjectName,
        HoursPerWeek = item.HoursPerWeek,
        DifficultyScore = item.DifficultyScore,
        HasSubgroups = item.HasSubgroups,
        WeekParity = item.WeekParity,
        ItemGradeFrom = item.ItemGradeFrom,
        ItemGradeTo = item.ItemGradeTo
    };

    private static async Task EnsureUserTemplateAsync(SqliteConnection conn, int id)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT is_builtin FROM curriculum_templates WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        var result = await cmd.ExecuteScalarAsync();
        if (result is null)
            throw new InvalidOperationException("Шаблон не найден");
        if (Convert.ToInt32(result) == 1)
            throw new InvalidOperationException("Встроенный шаблон нельзя изменить");
    }

    private static async Task<int> GetNextSortOrderAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(sort_order), -1) + 1 FROM curriculum_templates";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task<int> InsertHeaderAsync(
        SqliteConnection conn,
        string name,
        int gradeFrom,
        int gradeTo,
        bool isBuiltIn,
        int sortOrder)
    {
        await using var ins = conn.CreateCommand();
        ins.CommandText = """
            INSERT INTO curriculum_templates (name, grade_from, grade_to, is_builtin, sort_order)
            VALUES ($n, $gf, $gt, $ib, $so); SELECT last_insert_rowid();
            """;
        ins.Parameters.AddWithValue("$n", name.Trim());
        ins.Parameters.AddWithValue("$gf", gradeFrom);
        ins.Parameters.AddWithValue("$gt", gradeTo);
        ins.Parameters.AddWithValue("$ib", isBuiltIn ? 1 : 0);
        ins.Parameters.AddWithValue("$so", sortOrder);
        return Convert.ToInt32(await ins.ExecuteScalarAsync());
    }

    private static async Task ReplaceItemsAsync(
        SqliteConnection conn,
        int templateId,
        IReadOnlyList<CurriculumTemplateItem> items)
    {
        await using (var del = conn.CreateCommand())
        {
            del.CommandText = "DELETE FROM curriculum_template_items WHERE template_id = $id";
            del.Parameters.AddWithValue("$id", templateId);
            await del.ExecuteNonQueryAsync();
        }

        foreach (var item in items)
        {
            await using var row = conn.CreateCommand();
            row.CommandText = """
                INSERT INTO curriculum_template_items
                    (template_id, subject_name, hours_per_week, difficulty_score, has_subgroups, week_parity, item_grade_from, item_grade_to)
                VALUES ($t, $s, $h, $d, $g, $p, $gf, $gt)
                """;
            row.Parameters.AddWithValue("$t", templateId);
            row.Parameters.AddWithValue("$s", item.SubjectName.Trim());
            row.Parameters.AddWithValue("$h", item.HoursPerWeek);
            row.Parameters.AddWithValue("$d", item.DifficultyScore);
            row.Parameters.AddWithValue("$g", item.HasSubgroups ? 1 : 0);
            row.Parameters.AddWithValue("$p", item.WeekParity);
            row.Parameters.AddWithValue("$gf", item.ItemGradeFrom);
            row.Parameters.AddWithValue("$gt", item.ItemGradeTo);
            await row.ExecuteNonQueryAsync();
        }
    }
}
