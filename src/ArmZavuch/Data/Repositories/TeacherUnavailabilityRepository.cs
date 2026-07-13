using ArmZavuch.Models;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Repositories;

/// <summary>CRUD нерабочего времени сотрудника.</summary>
public sealed class TeacherUnavailabilityRepository
{
    private readonly SqliteConnectionFactory _factory;

    public TeacherUnavailabilityRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<List<TeacherUnavailability>> GetForTeacherAsync(int teacherId)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, teacher_id, recurrence_type, day_of_week, start_date, end_date,
                   all_day, lesson_from, lesson_to, note
            FROM teacher_unavailability
            WHERE teacher_id = $t
            ORDER BY recurrence_type, start_date
            """;
        cmd.Parameters.AddWithValue("$t", teacherId);
        return await ReadListAsync(cmd);
    }

    public async Task<List<TeacherUnavailability>> GetAllAsync()
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, teacher_id, recurrence_type, day_of_week, start_date, end_date,
                   all_day, lesson_from, lesson_to, note
            FROM teacher_unavailability
            """;
        return await ReadListAsync(cmd);
    }

    public async Task<int> InsertAsync(TeacherUnavailability item)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO teacher_unavailability
                (teacher_id, recurrence_type, day_of_week, start_date, end_date,
                 all_day, lesson_from, lesson_to, note)
            VALUES ($t, $r, $dow, $from, $to, $all, $lf, $lt, $n);
            SELECT last_insert_rowid();
            """;
        Bind(cmd, item);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task DeleteAsync(int id)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM teacher_unavailability WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    private static void Bind(SqliteCommand cmd, TeacherUnavailability item)
    {
        cmd.Parameters.AddWithValue("$t", item.TeacherId);
        cmd.Parameters.AddWithValue("$r", item.RecurrenceType);
        cmd.Parameters.AddWithValue("$dow", (object?)item.DayOfWeek ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$from", item.StartDate);
        cmd.Parameters.AddWithValue("$to", (object?)item.EndDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$all", item.AllDay ? 1 : 0);
        cmd.Parameters.AddWithValue("$lf", (object?)item.LessonFrom ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$lt", (object?)item.LessonTo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$n", (object?)item.Note ?? DBNull.Value);
    }

    private static async Task<List<TeacherUnavailability>> ReadListAsync(SqliteCommand cmd)
    {
        var list = new List<TeacherUnavailability>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new TeacherUnavailability
            {
                Id = reader.GetInt32(0),
                TeacherId = reader.GetInt32(1),
                RecurrenceType = reader.GetString(2),
                DayOfWeek = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                StartDate = reader.GetString(4),
                EndDate = reader.IsDBNull(5) ? null : reader.GetString(5),
                AllDay = reader.GetInt32(6) == 1,
                LessonFrom = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                LessonTo = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                Note = reader.IsDBNull(9) ? null : reader.GetString(9)
            });
        }
        return list;
    }
}
