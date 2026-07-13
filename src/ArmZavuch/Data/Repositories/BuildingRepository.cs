using ArmZavuch.Models;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Repositories;

/// <summary>CRUD зданий и маршрутов между ними.</summary>
public sealed class BuildingRepository
{
    private readonly SqliteConnectionFactory _factory;

    public BuildingRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<List<Building>> GetAllAsync()
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, color_hex FROM buildings ORDER BY name";
        return await ReadBuildingsAsync(cmd);
    }

    public async Task<int> InsertAsync(Building item)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO buildings (name, color_hex) VALUES ($n, $c); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$n", item.Name);
        cmd.Parameters.AddWithValue("$c", item.ColorHex);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task UpdateAsync(Building item)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE buildings SET name = $n, color_hex = $c WHERE id = $id";
        cmd.Parameters.AddWithValue("$n", item.Name);
        cmd.Parameters.AddWithValue("$c", item.ColorHex);
        cmd.Parameters.AddWithValue("$id", item.Id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var result = await TryDeleteAsync(id);
        if (!result.Success)
            throw new InvalidOperationException(result.ErrorMessage);
    }

    /// <summary>Удаляет здание вместе с маршрутами и кабинетами, если они не заняты в расписании.</summary>
    public async Task<BuildingDeleteResult> TryDeleteAsync(int buildingId)
    {
        await using var conn = _factory.CreateConnection();
        await using var nameCmd = conn.CreateCommand();
        nameCmd.CommandText = "SELECT name FROM buildings WHERE id = $id";
        nameCmd.Parameters.AddWithValue("$id", buildingId);
        var nameObj = await nameCmd.ExecuteScalarAsync();
        if (nameObj is null)
            return BuildingDeleteResult.Fail("Здание не найдено.");
        var buildingName = (string)nameObj;

        var scheduleSlots = await CountAsync(conn,
            """
            SELECT COUNT(*) FROM week_template_slots wts
            JOIN rooms r ON r.id = wts.room_id
            WHERE r.building_id = $id
            """,
            buildingId);
        if (scheduleSlots > 0)
        {
            return BuildingDeleteResult.Fail(
                $"Здание «{buildingName}» нельзя удалить: его кабинеты стоят в недельном расписании ({scheduleSlots} урок(ов)). " +
                "Сначала переназначьте кабинеты в конструкторе.");
        }

        var dayOverrides = await CountAsync(conn,
            """
            SELECT COUNT(*) FROM day_overrides d
            JOIN rooms r ON r.id = d.room_id
            WHERE r.building_id = $id
            """,
            buildingId);
        if (dayOverrides > 0)
        {
            return BuildingDeleteResult.Fail(
                $"Здание «{buildingName}» нельзя удалить: его кабинеты указаны в правках дня ({dayOverrides}). " +
                "Сначала измените или удалите эти правки в диспетчерской.");
        }

        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync();
        try
        {
            await ExecAsync(conn, tx,
                "DELETE FROM building_routes WHERE from_building_id = $id OR to_building_id = $id",
                buildingId);
            await ExecAsync(conn, tx,
                """
                UPDATE teachers SET room_id = NULL
                WHERE room_id IN (SELECT id FROM rooms WHERE building_id = $id)
                """,
                buildingId);
            await ExecAsync(conn, tx, "UPDATE school_classes SET building_id = NULL WHERE building_id = $id", buildingId);
            await ExecAsync(conn, tx, "DELETE FROM rooms WHERE building_id = $id", buildingId);
            await ExecAsync(conn, tx, "DELETE FROM buildings WHERE id = $id", buildingId);
            await tx.CommitAsync();
            return BuildingDeleteResult.Ok();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private static async Task<int> CountAsync(SqliteConnection conn, string sql, int buildingId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$id", buildingId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task ExecAsync(SqliteConnection conn, SqliteTransaction tx, string sql, int buildingId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$id", buildingId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<BuildingRoute>> GetRoutesAsync()
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT r.id, r.from_building_id, r.to_building_id, r.minutes,
                   b1.name, b2.name
            FROM building_routes r
            JOIN buildings b1 ON b1.id = r.from_building_id
            JOIN buildings b2 ON b2.id = r.to_building_id
            ORDER BY b1.name, b2.name
            """;
        var list = new List<BuildingRoute>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new BuildingRoute
            {
                Id = reader.GetInt32(0),
                FromBuildingId = reader.GetInt32(1),
                ToBuildingId = reader.GetInt32(2),
                Minutes = reader.GetInt32(3),
                FromBuildingName = reader.GetString(4),
                ToBuildingName = reader.GetString(5)
            });
        }
        return list;
    }

    public async Task InsertRouteAsync(int fromId, int toId, int minutes)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO building_routes (from_building_id, to_building_id, minutes)
            VALUES ($f, $t, $m)
            ON CONFLICT(from_building_id, to_building_id) DO UPDATE SET minutes = $m
            """;
        cmd.Parameters.AddWithValue("$f", fromId);
        cmd.Parameters.AddWithValue("$t", toId);
        cmd.Parameters.AddWithValue("$m", minutes);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Создаёт переходы 40 мин между новым зданием и остальными, если ещё не заданы.</summary>
    public async Task EnsureDefaultRoutesForBuildingAsync(int buildingId, int defaultMinutes = BuildingRouteDefaults.Minutes)
    {
        var buildings = await GetAllAsync();
        if (buildings.All(b => b.Id != buildingId))
            return;

        var existing = await GetRoutesAsync();
        foreach (var other in buildings.Where(b => b.Id != buildingId))
        {
            if (!existing.Any(r => r.FromBuildingId == buildingId && r.ToBuildingId == other.Id))
                await InsertRouteAsync(buildingId, other.Id, defaultMinutes);
            if (!existing.Any(r => r.FromBuildingId == other.Id && r.ToBuildingId == buildingId))
                await InsertRouteAsync(other.Id, buildingId, defaultMinutes);
        }
    }

    private static async Task<List<Building>> ReadBuildingsAsync(SqliteCommand cmd)
    {
        var list = new List<Building>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Building
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                ColorHex = reader.GetString(2)
            });
        }
        return list;
    }
}
