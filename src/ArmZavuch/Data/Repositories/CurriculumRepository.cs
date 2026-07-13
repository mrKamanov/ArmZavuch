using ArmZavuch.Models;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Repositories;

/// <summary>CRUD учебной нагрузки; балл Сивкова хранится на строке curriculum.</summary>
public sealed class CurriculumRepository
{
    private readonly SqliteConnectionFactory _factory;

    public CurriculumRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<List<CurriculumItem>> GetAllAsync()
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT cu.id, cu.class_id, c.grade || c.letter, cu.subject_id, s.name,
                   COALESCE(cu.difficulty_score, s.difficulty_score),
                   cu.hours_per_week, cu.has_subgroups, cu.week_parity
            FROM curriculum cu
            JOIN school_classes c ON c.id = cu.class_id
            JOIN subjects s ON s.id = cu.subject_id
            ORDER BY c.grade, c.letter, s.name, cu.week_parity
            """;
        var list = new List<CurriculumItem>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new CurriculumItem
            {
                Id = reader.GetInt32(0),
                ClassId = reader.GetInt32(1),
                ClassName = reader.GetString(2),
                SubjectId = reader.GetInt32(3),
                SubjectName = reader.GetString(4),
                SubjectDifficultyScore = reader.GetDouble(5),
                HoursPerWeek = reader.GetDouble(6),
                HasSubgroups = reader.GetInt32(7) == 1,
                WeekParity = reader.IsDBNull(8) ? CurriculumWeekParity.EveryWeek : reader.GetString(8)
            });
        }
        return list;
    }

    public async Task<int> InsertAsync(CurriculumItem item)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO curriculum (class_id, subject_id, hours_per_week, has_subgroups, week_parity, difficulty_score)
            VALUES ($c, $s, $h, $g, $p, $d); SELECT last_insert_rowid();
            """;
        BindCommon(cmd, item);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<bool> UpsertAsync(CurriculumItem item)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO curriculum (class_id, subject_id, hours_per_week, has_subgroups, week_parity, difficulty_score)
            VALUES ($c, $s, $h, $g, $p, $d)
            ON CONFLICT(class_id, subject_id, week_parity) DO UPDATE SET
                hours_per_week = excluded.hours_per_week,
                has_subgroups = excluded.has_subgroups,
                difficulty_score = excluded.difficulty_score
            """;
        BindCommon(cmd, item);
        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    public async Task UpdateAsync(CurriculumItem item)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE curriculum SET class_id = $c, subject_id = $s, hours_per_week = $h,
                has_subgroups = $g, week_parity = $p, difficulty_score = $d
            WHERE id = $id
            """;
        BindCommon(cmd, item);
        cmd.Parameters.AddWithValue("$id", item.Id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateHoursAsync(int id, double hoursPerWeek)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE curriculum SET hours_per_week = $h WHERE id = $id";
        cmd.Parameters.AddWithValue("$h", hoursPerWeek);
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM curriculum WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<CurriculumItem>> DeleteByClassIdAsync(int classId)
    {
        var existing = (await GetAllAsync()).Where(c => c.ClassId == classId).ToList();
        if (existing.Count == 0)
            return existing;

        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM curriculum WHERE class_id = $id";
        cmd.Parameters.AddWithValue("$id", classId);
        await cmd.ExecuteNonQueryAsync();
        return existing;
    }

    private static void BindCommon(SqliteCommand cmd, CurriculumItem item)
    {
        cmd.Parameters.AddWithValue("$c", item.ClassId);
        cmd.Parameters.AddWithValue("$s", item.SubjectId);
        cmd.Parameters.AddWithValue("$h", item.HoursPerWeek);
        cmd.Parameters.AddWithValue("$g", item.HasSubgroups ? 1 : 0);
        cmd.Parameters.AddWithValue("$p", item.WeekParity);
        cmd.Parameters.AddWithValue("$d", item.SubjectDifficultyScore);
    }
}
