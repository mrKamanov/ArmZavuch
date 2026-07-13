using ArmZavuch.Models;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Repositories;

/// <summary>CRUD классов: здание, кабинет и зал физ-ры по умолчанию; удаление с очисткой связей.</summary>
public sealed class SchoolClassRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SchoolClassRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<List<SchoolClass>> GetAllAsync()
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT sc.id, sc.grade, sc.letter, sc.shift, sc.student_count, sc.is_correctional,
                   sc.building_id, cb.name, cb.color_hex,
                   sc.default_room_id, dr.number, db.name,
                   sc.default_pe_room_id, pr.number, pb.name,
                   sc.bell_template_id, bt.name
            FROM school_classes sc
            LEFT JOIN buildings cb ON cb.id = sc.building_id
            LEFT JOIN rooms dr ON dr.id = sc.default_room_id
            LEFT JOIN buildings db ON db.id = dr.building_id
            LEFT JOIN rooms pr ON pr.id = sc.default_pe_room_id
            LEFT JOIN buildings pb ON pb.id = pr.building_id
            LEFT JOIN bell_templates bt ON bt.id = sc.bell_template_id
            ORDER BY sc.grade, sc.letter
            """;
        var list = new List<SchoolClass>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new SchoolClass
            {
                Id = reader.GetInt32(0),
                Grade = reader.GetInt32(1),
                Letter = reader.GetString(2),
                Shift = reader.GetInt32(3),
                StudentCount = reader.GetInt32(4),
                IsCorrectional = reader.GetInt32(5) != 0,
                BuildingId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                BuildingName = reader.IsDBNull(7) ? "" : reader.GetString(7),
                BuildingColorHex = reader.IsDBNull(8) ? "" : reader.GetString(8),
                DefaultRoomId = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                DefaultRoomDisplay = FormatRoomDisplay(
                    reader.IsDBNull(10) ? "" : reader.GetString(10),
                    reader.IsDBNull(11) ? "" : reader.GetString(11)),
                DefaultPeRoomId = reader.IsDBNull(12) ? null : reader.GetInt32(12),
                DefaultPeRoomDisplay = FormatRoomDisplay(
                    reader.IsDBNull(13) ? "" : reader.GetString(13),
                    reader.IsDBNull(14) ? "" : reader.GetString(14)),
                BellTemplateId = reader.IsDBNull(15) ? null : reader.GetInt32(15),
                BellTemplateName = reader.IsDBNull(16) ? "" : reader.GetString(16)
            });
        }
        return list;
    }

    public async Task<int> InsertAsync(SchoolClass item)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO school_classes (grade, letter, shift, student_count, is_correctional, building_id, default_room_id, default_pe_room_id, bell_template_id)
            VALUES ($g, $l, $s, $c, $ic, $b, $dr, $pe, $bt);
            SELECT last_insert_rowid();
            """;
        BindClass(cmd, item);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task UpdateAsync(SchoolClass item)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE school_classes
            SET grade = $g, letter = $l, shift = $s, student_count = $c,
                is_correctional = $ic, building_id = $b, default_room_id = $dr,
                default_pe_room_id = $pe, bell_template_id = $bt
            WHERE id = $id
            """;
        BindClass(cmd, item);
        cmd.Parameters.AddWithValue("$id", item.Id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var result = await TryDeleteAsync(id);
        if (!result.Success)
            throw new InvalidOperationException(result.ErrorMessage);
    }

    /// <summary>Удаляет класс и связанную нагрузку, слоты шаблонов, правки дня и записи замен.</summary>
    public async Task<BuildingDeleteResult> TryDeleteAsync(int classId)
    {
        await using var conn = _factory.CreateConnection();
        await using var nameCmd = conn.CreateCommand();
        nameCmd.CommandText = "SELECT grade, letter FROM school_classes WHERE id = $id";
        nameCmd.Parameters.AddWithValue("$id", classId);
        await using var reader = await nameCmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return BuildingDeleteResult.Fail("Класс не найден.");
        var displayName = $"{reader.GetInt32(0)}{reader.GetString(1)}";

        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync();
        try
        {
            await ExecAsync(conn, tx,
                """
                DELETE FROM teacher_curriculum_items
                WHERE curriculum_id IN (SELECT id FROM curriculum WHERE class_id = $id)
                """,
                classId);
            await ExecAsync(conn, tx, "DELETE FROM teacher_class_subjects WHERE class_id = $id", classId);
            await ExecAsync(conn, tx, "DELETE FROM teacher_preferred_classes WHERE class_id = $id", classId);
            await ExecAsync(conn, tx, "DELETE FROM class_teachers WHERE class_id = $id", classId);
            await ExecAsync(conn, tx, "DELETE FROM week_template_slots WHERE class_id = $id", classId);
            await ExecAsync(conn, tx, "DELETE FROM curriculum WHERE class_id = $id", classId);
            await ExecAsync(conn, tx,
                "DELETE FROM day_overrides WHERE class_id = $id OR target_class_id = $id",
                classId);
            await ExecAsync(conn, tx, "DELETE FROM substitution_records WHERE class_id = $id", classId);
            await ExecAsync(conn, tx, "DELETE FROM school_classes WHERE id = $id", classId);
            await tx.CommitAsync();
            return BuildingDeleteResult.Ok();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return BuildingDeleteResult.Fail(
                $"Класс «{displayName}» не удалось удалить: {ex.Message}");
        }
    }

    private static async Task ExecAsync(SqliteConnection conn, SqliteTransaction tx, string sql, int classId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$id", classId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int?> FindIdByDisplayNameAsync(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return null;

        var trimmed = displayName.Trim();
        foreach (var cls in await GetAllAsync())
        {
            if (cls.DisplayName.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
                return cls.Id;
        }

        return null;
    }

    private static void BindClass(SqliteCommand cmd, SchoolClass item)
    {
        cmd.Parameters.AddWithValue("$g", item.Grade);
        cmd.Parameters.AddWithValue("$l", item.Letter);
        cmd.Parameters.AddWithValue("$s", item.Shift);
        cmd.Parameters.AddWithValue("$c", item.StudentCount);
        cmd.Parameters.AddWithValue("$ic", item.IsCorrectional ? 1 : 0);
        cmd.Parameters.AddWithValue("$b", (object?)item.BuildingId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$dr", (object?)item.DefaultRoomId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$pe", (object?)item.DefaultPeRoomId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$bt", (object?)item.BellTemplateId ?? DBNull.Value);
    }

    private static string FormatRoomDisplay(string roomNumber, string buildingName)
    {
        if (string.IsNullOrWhiteSpace(roomNumber))
            return "";
        return string.IsNullOrWhiteSpace(buildingName)
            ? roomNumber
            : $"{roomNumber} · {buildingName}";
    }
}
