using ArmZavuch.Models;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Repositories;

/// <summary>CRUD периодов отсутствия (больничный, отгул, открытый период).</summary>
public sealed class TeacherStatusRepository
{
    private readonly SqliteConnectionFactory _factory;

    public TeacherStatusRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<List<TeacherStatusPeriod>> GetForTeacherAsync(int teacherId)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, teacher_id, status_type, start_date, end_date, note, is_official, source
            FROM teacher_status_periods
            WHERE teacher_id = $t
            ORDER BY start_date DESC, id DESC
            """;
        cmd.Parameters.AddWithValue("$t", teacherId);
        return await ReadListAsync(cmd);
    }

    public async Task<List<TeacherStatusPeriod>> GetActiveForDateAsync(DateOnly date)
    {
        var d = date.ToString("yyyy-MM-dd");
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, teacher_id, status_type, start_date, end_date, note, is_official, source
            FROM teacher_status_periods
            WHERE start_date <= $d AND (end_date IS NULL OR end_date >= $d)
            ORDER BY start_date DESC, id DESC
            """;
        cmd.Parameters.AddWithValue("$d", d);
        return await ReadListAsync(cmd);
    }

    public async Task<TeacherStatusPeriod?> GetActiveForTeacherOnDateAsync(int teacherId, DateOnly date)
    {
        var d = date.ToString("yyyy-MM-dd");
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, teacher_id, status_type, start_date, end_date, note, is_official, source
            FROM teacher_status_periods
            WHERE teacher_id = $t AND start_date <= $d AND (end_date IS NULL OR end_date >= $d)
            ORDER BY id DESC
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$t", teacherId);
        cmd.Parameters.AddWithValue("$d", d);
        var list = await ReadListAsync(cmd);
        return list.FirstOrDefault();
    }

    public async Task<int> InsertAsync(TeacherStatusPeriod item)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO teacher_status_periods
                (teacher_id, status_type, start_date, end_date, note, is_official, source)
            VALUES ($t, $s, $from, $to, $n, $o, $src);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$t", item.TeacherId);
        cmd.Parameters.AddWithValue("$s", item.StatusType);
        cmd.Parameters.AddWithValue("$from", item.StartDate);
        cmd.Parameters.AddWithValue("$to", (object?)item.EndDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$n", (object?)item.Note ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$o", item.IsOfficial ? 1 : 0);
        cmd.Parameters.AddWithValue("$src", item.Source);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task UpdateEndDateAsync(int id, string? endDate)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE teacher_status_periods SET end_date = $e WHERE id = $id";
        cmd.Parameters.AddWithValue("$e", (object?)endDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM teacher_status_periods WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<TeacherStatusPeriod>> GetOverlappingRangeAsync(DateOnly from, DateOnly to)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, teacher_id, status_type, start_date, end_date, note, is_official, source
            FROM teacher_status_periods
            WHERE start_date <= $to AND (end_date IS NULL OR end_date >= $from)
            ORDER BY start_date DESC, teacher_id, id DESC
            """;
        cmd.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$to", to.ToString("yyyy-MM-dd"));
        return await ReadListAsync(cmd);
    }

    private static async Task<List<TeacherStatusPeriod>> ReadListAsync(SqliteCommand cmd)
    {
        var list = new List<TeacherStatusPeriod>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new TeacherStatusPeriod
            {
                Id = reader.GetInt32(0),
                TeacherId = reader.GetInt32(1),
                StatusType = reader.GetString(2),
                StartDate = reader.GetString(3),
                EndDate = reader.IsDBNull(4) ? null : reader.GetString(4),
                Note = reader.IsDBNull(5) ? null : reader.GetString(5),
                IsOfficial = !reader.IsDBNull(6) && reader.GetInt32(6) != 0,
                Source = reader.IsDBNull(7) ? AbsenceSources.Profile : reader.GetString(7)
            });
        }
        return list;
    }
}
