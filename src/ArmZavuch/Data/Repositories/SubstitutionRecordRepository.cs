using ArmZavuch.Models;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Repositories;

/// <summary>Журнал замен: назначения из диспетчерской и ручные записи.</summary>
public sealed class SubstitutionRecordRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SubstitutionRecordRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<int> InsertAsync(SubstitutionRecord item)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO substitution_records
                (date, lesson_number, class_id, class_name, class_shift, subject_id, subject_name,
                 absent_teacher_id, absent_teacher_name, replacement_teacher_id, replacement_teacher_name,
                 start_time, end_time, is_official, source, note, day_override_id)
            VALUES ($d, $l, $c, $cn, $sh, $s, $sn, $at, $atn, $rt, $rtn, $st, $et, $o, $src, $n, $oid);
            SELECT last_insert_rowid();
            """;
        Bind(cmd, item);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<List<SubstitutionRecord>> GetForDateRangeAsync(DateOnly from, DateOnly to)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, date, lesson_number, class_id, class_name, class_shift, subject_id, subject_name,
                   absent_teacher_id, absent_teacher_name, replacement_teacher_id, replacement_teacher_name,
                   start_time, end_time, is_official, source, note, day_override_id
            FROM substitution_records
            WHERE date >= $from AND date <= $to
            ORDER BY date, lesson_number, class_name
            """;
        cmd.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$to", to.ToString("yyyy-MM-dd"));
        return await ReadListAsync(cmd);
    }

    public async Task DeleteAsync(int id)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM substitution_records WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteByDayOverrideIdAsync(int dayOverrideId)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM substitution_records WHERE day_override_id = $id";
        cmd.Parameters.AddWithValue("$id", dayOverrideId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteForDateAsync(string date)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM substitution_records WHERE date = $d";
        cmd.Parameters.AddWithValue("$d", date);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> CountForDateAsync(string date)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM substitution_records WHERE date = $d";
        cmd.Parameters.AddWithValue("$d", date);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task DeleteForLessonAsync(string date, int absentTeacherId, int lessonNumber, int classId)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM substitution_records
            WHERE date = $d AND absent_teacher_id = $t AND lesson_number = $l AND class_id = $c
            """;
        cmd.Parameters.AddWithValue("$d", date);
        cmd.Parameters.AddWithValue("$t", absentTeacherId);
        cmd.Parameters.AddWithValue("$l", lessonNumber);
        cmd.Parameters.AddWithValue("$c", classId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static void Bind(SqliteCommand cmd, SubstitutionRecord item)
    {
        cmd.Parameters.AddWithValue("$d", item.Date);
        cmd.Parameters.AddWithValue("$l", item.LessonNumber);
        cmd.Parameters.AddWithValue("$c", (object?)item.ClassId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$cn", item.ClassName);
        cmd.Parameters.AddWithValue("$sh", item.ClassShift);
        cmd.Parameters.AddWithValue("$s", (object?)item.SubjectId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$sn", item.SubjectName);
        cmd.Parameters.AddWithValue("$at", item.AbsentTeacherId);
        cmd.Parameters.AddWithValue("$atn", item.AbsentTeacherName);
        cmd.Parameters.AddWithValue("$rt", item.ReplacementTeacherId);
        cmd.Parameters.AddWithValue("$rtn", item.ReplacementTeacherName);
        cmd.Parameters.AddWithValue("$st", item.StartTime);
        cmd.Parameters.AddWithValue("$et", item.EndTime);
        cmd.Parameters.AddWithValue("$o", item.IsOfficial ? 1 : 0);
        cmd.Parameters.AddWithValue("$src", item.Source);
        cmd.Parameters.AddWithValue("$n", (object?)item.Note ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$oid", (object?)item.DayOverrideId ?? DBNull.Value);
    }

    private static async Task<List<SubstitutionRecord>> ReadListAsync(SqliteCommand cmd)
    {
        var list = new List<SubstitutionRecord>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new SubstitutionRecord
            {
                Id = reader.GetInt32(0),
                Date = reader.GetString(1),
                LessonNumber = reader.GetInt32(2),
                ClassId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                ClassName = reader.IsDBNull(4) ? "" : reader.GetString(4),
                ClassShift = reader.IsDBNull(5) ? 1 : reader.GetInt32(5),
                SubjectId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                SubjectName = reader.IsDBNull(7) ? "" : reader.GetString(7),
                AbsentTeacherId = reader.GetInt32(8),
                AbsentTeacherName = reader.IsDBNull(9) ? "" : reader.GetString(9),
                ReplacementTeacherId = reader.GetInt32(10),
                ReplacementTeacherName = reader.IsDBNull(11) ? "" : reader.GetString(11),
                StartTime = reader.IsDBNull(12) ? "" : reader.GetString(12),
                EndTime = reader.IsDBNull(13) ? "" : reader.GetString(13),
                IsOfficial = !reader.IsDBNull(14) && reader.GetInt32(14) != 0,
                Source = reader.IsDBNull(15) ? AbsenceSources.Dispatcher : reader.GetString(15),
                Note = reader.IsDBNull(16) ? null : reader.GetString(16),
                DayOverrideId = reader.IsDBNull(17) ? null : reader.GetInt32(17)
            });
        }
        return list;
    }
}
