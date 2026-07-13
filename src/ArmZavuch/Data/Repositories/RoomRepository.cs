using ArmZavuch.Models;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Repositories;

/// <summary>CRUD кабинетов.</summary>
public sealed class RoomRepository
{
    private readonly SqliteConnectionFactory _factory;

    public RoomRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<List<Room>> GetAllAsync()
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT r.id, r.number, r.building_id, b.name, b.color_hex, r.capacity, r.room_kind,
                   r.assigned_teacher_id, t.full_name, r.allows_parallel_groups
            FROM rooms r
            JOIN buildings b ON b.id = r.building_id
            LEFT JOIN teachers t ON t.id = r.assigned_teacher_id
            ORDER BY b.name, r.number
            """;
        var list = new List<Room>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(ReadRoom(reader));
        }
        return list;
    }

    public async Task<int> InsertAsync(Room item)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO rooms (number, building_id, capacity, room_kind, assigned_teacher_id, allows_parallel_groups)
            VALUES ($n, $b, $c, $k, $t, $p); SELECT last_insert_rowid();
            """;
        Bind(cmd, item);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task UpdateAsync(Room item)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE rooms SET number = $n, building_id = $b, capacity = $c, room_kind = $k,
                assigned_teacher_id = $t, allows_parallel_groups = $p
            WHERE id = $id
            """;
        Bind(cmd, item);
        cmd.Parameters.AddWithValue("$id", item.Id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM rooms WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    private static Room ReadRoom(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Number = reader.GetString(1),
        BuildingId = reader.GetInt32(2),
        BuildingName = reader.GetString(3),
        BuildingColorHex = BuildingColors.Normalize(reader.GetString(4)),
        Capacity = reader.GetInt32(5),
        RoomKind = reader.GetString(6),
        AssignedTeacherId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
        AssignedTeacherName = reader.IsDBNull(8) ? null : reader.GetString(8),
        AllowsParallelGroups = !reader.IsDBNull(9) && reader.GetInt32(9) != 0
    };

    private static void Bind(SqliteCommand cmd, Room item)
    {
        cmd.Parameters.AddWithValue("$n", item.Number);
        cmd.Parameters.AddWithValue("$b", item.BuildingId);
        cmd.Parameters.AddWithValue("$c", item.Capacity);
        cmd.Parameters.AddWithValue("$k", item.RoomKind);
        cmd.Parameters.AddWithValue("$t", (object?)item.AssignedTeacherId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$p", item.AllowsParallelGroups ? 1 : 0);
    }
}
