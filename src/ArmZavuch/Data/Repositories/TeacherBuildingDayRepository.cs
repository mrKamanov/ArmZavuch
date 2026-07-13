using ArmZavuch.Models;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Repositories;

/// <summary>CRUD: здание педагога по дню недели.</summary>
public sealed class TeacherBuildingDayRepository
{
    private readonly SqliteConnectionFactory _factory;

    public TeacherBuildingDayRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<List<TeacherBuildingDay>> GetForTeacherAsync(int teacherId)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT tbd.id, tbd.teacher_id, tbd.day_of_week, tbd.building_id, b.name
            FROM teacher_building_days tbd
            JOIN buildings b ON b.id = tbd.building_id
            WHERE tbd.teacher_id = $tid
            ORDER BY tbd.day_of_week
            """;
        cmd.Parameters.AddWithValue("$tid", teacherId);
        return await ReadListAsync(cmd);
    }

    public async Task<List<TeacherBuildingDay>> GetAllAsync()
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT tbd.id, tbd.teacher_id, tbd.day_of_week, tbd.building_id, b.name
            FROM teacher_building_days tbd
            JOIN buildings b ON b.id = tbd.building_id
            ORDER BY tbd.teacher_id, tbd.day_of_week
            """;
        return await ReadListAsync(cmd);
    }

    public async Task<int> InsertAsync(TeacherBuildingDay item)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO teacher_building_days (teacher_id, day_of_week, building_id)
            VALUES ($tid, $dow, $bid);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$tid", item.TeacherId);
        cmd.Parameters.AddWithValue("$dow", item.DayOfWeek);
        cmd.Parameters.AddWithValue("$bid", item.BuildingId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task DeleteAsync(int id)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM teacher_building_days WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<List<TeacherBuildingDay>> ReadListAsync(SqliteCommand cmd)
    {
        var list = new List<TeacherBuildingDay>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new TeacherBuildingDay
            {
                Id = reader.GetInt32(0),
                TeacherId = reader.GetInt32(1),
                DayOfWeek = reader.GetInt32(2),
                BuildingId = reader.GetInt32(3),
                BuildingName = reader.GetString(4)
            });
        }
        return list;
    }
}
