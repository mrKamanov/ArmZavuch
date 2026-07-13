using ArmZavuch.Models;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Repositories;

/// <summary>Суточные оперативные правки (ТЗ: DayOverride).</summary>
public sealed class DayOverrideRepository
{
    private readonly SqliteConnectionFactory _factory;

    public DayOverrideRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<int> InsertAsync(string date, string type, int? teacherId = null, int? classId = null,
        int? lessonNumber = null, int? replacementTeacherId = null, string? note = null, int? bellTemplateId = null,
        int? targetClassId = null, int? targetLessonNumber = null, int? roomId = null, bool clearRoom = false)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO day_overrides
                (date, override_type, teacher_id, class_id, lesson_number, replacement_teacher_id,
                 bell_template_id, note, target_class_id, target_lesson_number, room_id, clear_room)
            VALUES ($d, $t, $te, $c, $l, $r, $b, $n, $tc, $tl, $rm, $cr);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$d", date);
        cmd.Parameters.AddWithValue("$t", type);
        cmd.Parameters.AddWithValue("$te", (object?)teacherId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$c", (object?)classId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$l", (object?)lessonNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$r", (object?)replacementTeacherId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$b", (object?)bellTemplateId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$n", (object?)note ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tc", (object?)targetClassId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tl", (object?)targetLessonNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$rm", (object?)roomId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$cr", clearRoom ? 1 : 0);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<List<DayOverrideRecord>> GetRecordsForDateAsync(string date)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, date, override_type, class_id, lesson_number, teacher_id, replacement_teacher_id,
                   room_id, bell_template_id, target_class_id, target_lesson_number, note, clear_room
            FROM day_overrides WHERE date = $d
            ORDER BY id
            """;
        cmd.Parameters.AddWithValue("$d", date);
        var list = new List<DayOverrideRecord>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new DayOverrideRecord
            {
                Id = reader.GetInt32(0),
                Date = reader.GetString(1),
                OverrideType = reader.GetString(2),
                ClassId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                LessonNumber = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                TeacherId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                ReplacementTeacherId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                RoomId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                BellTemplateId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                TargetClassId = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                TargetLessonNumber = reader.IsDBNull(10) ? null : reader.GetInt32(10),
                Note = reader.IsDBNull(11) ? null : reader.GetString(11),
                ClearRoom = !reader.IsDBNull(12) && reader.GetInt32(12) != 0
            });
        }
        return list;
    }

    public async Task DeleteAsync(int id)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM day_overrides WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAllForDateAsync(string date)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM day_overrides WHERE date = $d";
        cmd.Parameters.AddWithValue("$d", date);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> CountForDateAsync(string date)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM day_overrides WHERE date = $d";
        cmd.Parameters.AddWithValue("$d", date);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<int> CountScheduleOverridesForDateAsync(string date)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM day_overrides
            WHERE date = $d AND override_type != 'DayNote'
            """;
        cmd.Parameters.AddWithValue("$d", date);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<DayOverrideRecord?> GetDayNoteAsync(string date)
    {
        var records = await GetRecordsForDateAsync(date);
        return records.LastOrDefault(r => r.OverrideType == "DayNote");
    }

    public async Task UpsertDayNoteAsync(string date, string? text)
    {
        var trimmed = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        var existing = await GetDayNoteAsync(date);
        if (trimmed is null)
        {
            if (existing is not null)
                await DeleteAsync(existing.Id);
            return;
        }

        if (existing is not null)
        {
            await UpdateNoteAsync(existing.Id, trimmed);
            return;
        }

        await InsertAsync(date, "DayNote", note: trimmed);
    }

    public async Task UpdateNoteAsync(int id, string note)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE day_overrides SET note = $n WHERE id = $id";
        cmd.Parameters.AddWithValue("$n", note);
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteSubstitutionsForDateAsync(string date)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM day_overrides
            WHERE date = $d AND override_type = 'Substitution'
            """;
        cmd.Parameters.AddWithValue("$d", date);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteScheduleOverridesForDateAsync(string date)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM day_overrides
            WHERE date = $d AND override_type != 'DayNote'
            """;
        cmd.Parameters.AddWithValue("$d", date);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<(string Type, int? TeacherId, int? ClassId, int? LessonNumber, int? ReplacementId,
            int? TargetClassId, int? TargetLessonNumber, string? Note)>>
        GetForDateAsync(string date)
    {
        var records = await GetRecordsForDateAsync(date);
        return records.Select(r => (
            r.OverrideType,
            r.TeacherId,
            r.ClassId,
            r.LessonNumber,
            r.ReplacementTeacherId,
            r.TargetClassId,
            r.TargetLessonNumber,
            r.Note)).ToList();
    }
}
