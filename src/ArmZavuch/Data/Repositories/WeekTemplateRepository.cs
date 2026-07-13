using ArmZavuch.Models;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Repositories;

/// <summary>Недельные шаблоны и слоты расписания (ТЗ §3).</summary>
public sealed class WeekTemplateRepository
{
    private readonly SqliteConnectionFactory _factory;

    public WeekTemplateRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<List<WeekTemplateInfo>> GetTemplatesAsync()
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, week_parity FROM week_templates ORDER BY name";
        var list = new List<WeekTemplateInfo>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new WeekTemplateInfo
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                WeekParity = reader.IsDBNull(2) ? WeekTemplateParity.Any : reader.GetString(2)
            });
        }
        return list;
    }

    public async Task<WeekTemplateInfo?> GetByIdAsync(int id)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, week_parity FROM week_templates WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;
        return new WeekTemplateInfo
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            WeekParity = reader.IsDBNull(2) ? WeekTemplateParity.Any : reader.GetString(2)
        };
    }

    public async Task DeleteAsync(int id)
    {
        await using var conn = _factory.CreateConnection();
        await using var slots = conn.CreateCommand();
        slots.CommandText = "DELETE FROM week_template_slots WHERE week_template_id = $id";
        slots.Parameters.AddWithValue("$id", id);
        await slots.ExecuteNonQueryAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM week_templates WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> NameExistsAsync(string name)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM week_templates WHERE lower(name) = lower($n) LIMIT 1";
        cmd.Parameters.AddWithValue("$n", name);
        return await cmd.ExecuteScalarAsync() is not null;
    }

    public async Task<int> CreateAsync(string name)
    {
        var parity = WeekTemplateParity.InferFromName(name);
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO week_templates (name, week_parity) VALUES ($n, $p);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$n", name);
        cmd.Parameters.AddWithValue("$p", parity);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task UpdateParityAsync(int id, string parity)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE week_templates SET week_parity = $p WHERE id = $id";
        cmd.Parameters.AddWithValue("$p", parity);
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> CreateFromCopyAsync(string newName, int sourceId)
    {
        var parity = WeekTemplateParity.InferFromName(newName);
        await using var conn = _factory.CreateConnection();
        await using var insert = conn.CreateCommand();
        insert.CommandText = """
            INSERT INTO week_templates (name, copied_from_id, week_parity) VALUES ($n, $s, $p);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("$n", newName);
        insert.Parameters.AddWithValue("$s", sourceId);
        insert.Parameters.AddWithValue("$p", parity);
        var newId = Convert.ToInt32(await insert.ExecuteScalarAsync());

        await using var copy = conn.CreateCommand();
        copy.CommandText = """
            INSERT INTO week_template_slots
                (week_template_id, day_of_week, lesson_number, class_id, subject_id, teacher_id, room_id, subgroup_index, is_anchored)
            SELECT $new, day_of_week, lesson_number, class_id, subject_id, teacher_id, room_id, subgroup_index, is_anchored
            FROM week_template_slots WHERE week_template_id = $src
            """;
        copy.Parameters.AddWithValue("$new", newId);
        copy.Parameters.AddWithValue("$src", sourceId);
        await copy.ExecuteNonQueryAsync();
        return newId;
    }

    public async Task<List<LessonSlot>> GetAllSlotsForTemplateAsync(int templateId)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT s.id, s.lesson_number, c.id, c.grade || c.letter, c.grade, c.shift,
                   sub.id, sub.name, t.id, t.full_name, r.id, r.number, b.name, s.subgroup_index, s.day_of_week,
                   s.is_anchored
            FROM week_template_slots s
            JOIN school_classes c ON c.id = s.class_id
            LEFT JOIN subjects sub ON sub.id = s.subject_id
            LEFT JOIN teachers t ON t.id = s.teacher_id
            LEFT JOIN rooms r ON r.id = s.room_id
            LEFT JOIN buildings b ON b.id = r.building_id
            WHERE s.week_template_id = $w
            """;
        cmd.Parameters.AddWithValue("$w", templateId);
        var list = new List<LessonSlot>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new LessonSlot
            {
                SlotId = reader.GetInt32(0),
                LessonNumber = reader.GetInt32(1),
                ClassId = reader.GetInt32(2),
                ClassName = reader.GetString(3),
                ClassGrade = reader.GetInt32(4),
                ClassShift = reader.GetInt32(5),
                SubjectId = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                SubjectName = reader.IsDBNull(7) ? "" : reader.GetString(7),
                TeacherId = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                TeacherName = reader.IsDBNull(9) ? "" : reader.GetString(9),
                RoomId = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                RoomNumber = reader.IsDBNull(11) ? "" : reader.GetString(11),
                BuildingName = reader.IsDBNull(12) ? "" : reader.GetString(12),
                SubgroupIndex = reader.GetInt32(13),
                DayOfWeek = reader.GetInt32(14),
                IsAnchored = ReadAnchored(reader, 15)
            });
        }
        return list;
    }

    public async Task UpsertSlotAsync(int templateId, int dayOfWeek, int lessonNumber, int classId,
        int? subjectId, int? teacherId, int? roomId, int subgroupIndex = 0)
    {
        await using var conn = _factory.CreateConnection();
        await using var find = conn.CreateCommand();
        find.CommandText = """
            SELECT id FROM week_template_slots
            WHERE week_template_id=$w AND day_of_week=$d AND lesson_number=$l
              AND class_id=$c AND subgroup_index=$g
            """;
        find.Parameters.AddWithValue("$w", templateId);
        find.Parameters.AddWithValue("$d", dayOfWeek);
        find.Parameters.AddWithValue("$l", lessonNumber);
        find.Parameters.AddWithValue("$c", classId);
        find.Parameters.AddWithValue("$g", subgroupIndex);
        var existingId = await find.ExecuteScalarAsync();

        if (existingId is not null and not DBNull)
        {
            await using var update = conn.CreateCommand();
            update.CommandText = """
                UPDATE week_template_slots SET subject_id=$s, teacher_id=$t, room_id=$r, is_anchored=0
                WHERE id=$id
                """;
            update.Parameters.AddWithValue("$s", subjectId.HasValue && subjectId.Value > 0 ? subjectId.Value : DBNull.Value);
            update.Parameters.AddWithValue("$t", teacherId is > 0 ? teacherId.Value : DBNull.Value);
            update.Parameters.AddWithValue("$r", (object?)roomId ?? DBNull.Value);
            update.Parameters.AddWithValue("$id", Convert.ToInt32(existingId));
            await update.ExecuteNonQueryAsync();
            return;
        }

        await using var insert = conn.CreateCommand();
        insert.CommandText = """
            INSERT INTO week_template_slots
                (week_template_id, day_of_week, lesson_number, class_id, subject_id, teacher_id, room_id, subgroup_index, is_anchored)
            VALUES ($w, $d, $l, $c, $s, $t, $r, $g, 0)
            """;
        insert.Parameters.AddWithValue("$w", templateId);
        insert.Parameters.AddWithValue("$d", dayOfWeek);
        insert.Parameters.AddWithValue("$l", lessonNumber);
        insert.Parameters.AddWithValue("$c", classId);
        insert.Parameters.AddWithValue("$s", subjectId.HasValue && subjectId.Value > 0 ? subjectId.Value : DBNull.Value);
        insert.Parameters.AddWithValue("$t", teacherId is > 0 ? teacherId.Value : DBNull.Value);
        insert.Parameters.AddWithValue("$r", (object?)roomId ?? DBNull.Value);
        insert.Parameters.AddWithValue("$g", subgroupIndex);
        await insert.ExecuteNonQueryAsync();
    }

    private static bool ReadAnchored(SqliteDataReader reader, int ordinal) =>
        !reader.IsDBNull(ordinal) && reader.GetInt32(ordinal) != 0;

    public async Task<WeekTemplateSlotRecord?> FindSlotAtAsync(
        int templateId, int dayOfWeek, int lessonNumber, int classId, int subgroupIndex = 0)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, subject_id, teacher_id, room_id, is_anchored
            FROM week_template_slots
            WHERE week_template_id=$w AND day_of_week=$d AND lesson_number=$l
              AND class_id=$c AND subgroup_index=$g
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$w", templateId);
        cmd.Parameters.AddWithValue("$d", dayOfWeek);
        cmd.Parameters.AddWithValue("$l", lessonNumber);
        cmd.Parameters.AddWithValue("$c", classId);
        cmd.Parameters.AddWithValue("$g", subgroupIndex);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new WeekTemplateSlotRecord
        {
            SlotId = reader.GetInt32(0),
            SubjectId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
            TeacherId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
            RoomId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
            IsAnchored = ReadAnchored(reader, 4)
        };
    }

    public async Task RelocateSlotLessonNumberAsync(int slotId, int lessonNumber)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE week_template_slots SET lesson_number = $l WHERE id = $id";
        cmd.Parameters.AddWithValue("$l", lessonNumber);
        cmd.Parameters.AddWithValue("$id", slotId);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Удаляет все слоты указанных классов в шаблоне (вся неделя).</summary>
    public async Task DeleteSlotsForClassesAsync(int templateId, IEnumerable<int> classIds)
    {
        var ids = classIds.Distinct().ToList();
        if (ids.Count == 0)
            return;

        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        var placeholders = string.Join(", ", ids.Select((_, i) => $"$c{i}"));
        cmd.CommandText = $"""
            DELETE FROM week_template_slots
            WHERE week_template_id = $w AND class_id IN ({placeholders})
            """;
        cmd.Parameters.AddWithValue("$w", templateId);
        for (var i = 0; i < ids.Count; i++)
            cmd.Parameters.AddWithValue($"$c{i}", ids[i]);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Удаляет слоты указанных классов в один день недели шаблона.</summary>
    public async Task DeleteSlotsForClassesOnDayAsync(int templateId, int dayOfWeek, IEnumerable<int> classIds)
    {
        var ids = classIds.Distinct().ToList();
        if (ids.Count == 0)
            return;

        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        var placeholders = string.Join(", ", ids.Select((_, i) => $"$c{i}"));
        cmd.CommandText = $"""
            DELETE FROM week_template_slots
            WHERE week_template_id = $w AND day_of_week = $d AND class_id IN ({placeholders})
            """;
        cmd.Parameters.AddWithValue("$w", templateId);
        cmd.Parameters.AddWithValue("$d", dayOfWeek);
        for (var i = 0; i < ids.Count; i++)
            cmd.Parameters.AddWithValue($"$c{i}", ids[i]);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteSlotAsync(int slotId)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM week_template_slots WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", slotId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteSlotAtAsync(int templateId, int dayOfWeek, int lessonNumber, int classId, int subgroupIndex = 0)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM week_template_slots
            WHERE week_template_id=$w AND day_of_week=$d AND lesson_number=$l AND class_id=$c AND subgroup_index=$g
            """;
        cmd.Parameters.AddWithValue("$w", templateId);
        cmd.Parameters.AddWithValue("$d", dayOfWeek);
        cmd.Parameters.AddWithValue("$l", lessonNumber);
        cmd.Parameters.AddWithValue("$c", classId);
        cmd.Parameters.AddWithValue("$g", subgroupIndex);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> ClearNonAnchoredTeacherSlotsAsync(int templateId, IReadOnlyList<int> teacherIds)
    {
        if (teacherIds.Count == 0)
            return 0;

        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        var placeholders = string.Join(", ", teacherIds.Select((_, i) => $"$t{i}"));
        cmd.CommandText = $"""
            DELETE FROM week_template_slots
            WHERE week_template_id=$w AND is_anchored=0 AND teacher_id IN ({placeholders})
            """;
        cmd.Parameters.AddWithValue("$w", templateId);
        for (var i = 0; i < teacherIds.Count; i++)
            cmd.Parameters.AddWithValue($"$t{i}", teacherIds[i]);
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<LessonSlot>> GetSlotsForTemplateDayAsync(int templateId, int dayOfWeek)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT s.id, s.lesson_number, c.id, c.grade || c.letter, c.grade, c.shift,
                   sub.id, sub.name, t.id, t.full_name, r.id, r.number, b.name, s.subgroup_index, s.is_anchored
            FROM week_template_slots s
            JOIN school_classes c ON c.id = s.class_id
            LEFT JOIN subjects sub ON sub.id = s.subject_id
            LEFT JOIN teachers t ON t.id = s.teacher_id
            LEFT JOIN rooms r ON r.id = s.room_id
            LEFT JOIN buildings b ON b.id = r.building_id
            WHERE s.week_template_id = $w AND s.day_of_week = $d
            ORDER BY s.lesson_number, c.grade, c.letter, s.subgroup_index
            """;
        cmd.Parameters.AddWithValue("$w", templateId);
        cmd.Parameters.AddWithValue("$d", dayOfWeek);
        var list = await ReadSlotsAsync(cmd, null);
        foreach (var slot in list)
            slot.DayOfWeek = dayOfWeek;
        return list;
    }

    public async Task<bool> HasTeacherClassSubjectSlotsAsync(int templateId, int teacherId, int classId, int subjectId)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT 1
            FROM week_template_slots
            WHERE week_template_id = $w AND teacher_id = $t AND class_id = $c AND subject_id = $s
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$w", templateId);
        cmd.Parameters.AddWithValue("$t", teacherId);
        cmd.Parameters.AddWithValue("$c", classId);
        cmd.Parameters.AddWithValue("$s", subjectId);
        return (await cmd.ExecuteScalarAsync()) is not null;
    }

    public async Task<List<LessonSlot>> ReadSlotsAsync(SqliteCommand cmd, DateOnly? date)
    {
        var list = new List<LessonSlot>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new LessonSlot
            {
                SlotId = reader.GetInt32(0),
                Date = date ?? default,
                LessonNumber = reader.GetInt32(1),
                ClassId = reader.GetInt32(2),
                ClassName = reader.GetString(3),
                ClassGrade = reader.GetInt32(4),
                ClassShift = reader.GetInt32(5),
                SubjectId = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                SubjectName = reader.IsDBNull(7) ? "" : reader.GetString(7),
                TeacherId = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                TeacherName = reader.IsDBNull(9) ? "" : reader.GetString(9),
                RoomId = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                RoomNumber = reader.IsDBNull(11) ? "" : reader.GetString(11),
                BuildingName = reader.IsDBNull(12) ? "" : reader.GetString(12),
                SubgroupIndex = reader.GetInt32(13),
                IsAnchored = ReadAnchored(reader, 14)
            });
        }
        return list;
    }

    public async Task<bool> HasTeacherClassSlotsAsync(int teacherId, int classId)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT 1 FROM week_template_slots
            WHERE teacher_id = $t AND class_id = $c
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$t", teacherId);
        cmd.Parameters.AddWithValue("$c", classId);
        return await cmd.ExecuteScalarAsync() is not null;
    }
}
