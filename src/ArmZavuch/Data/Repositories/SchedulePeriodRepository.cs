using ArmZavuch.Models;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Repositories;

/// <summary>CRUD периодов действия расписания.</summary>
public sealed class SchedulePeriodRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SchedulePeriodRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<List<SchedulePeriodInfo>> GetAllAsync()
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, period_type, start_date, end_date, recurrence_cycle FROM schedule_periods ORDER BY start_date";
        var list = new List<SchedulePeriodInfo>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new SchedulePeriodInfo
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                PeriodType = reader.GetString(2),
                StartDate = reader.GetString(3),
                EndDate = reader.GetString(4),
                RecurrenceCycle = reader.GetString(5)
            });
        }
        return list;
    }

    public async Task<int> InsertAsync(SchedulePeriodInfo item)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO schedule_periods (name, period_type, start_date, end_date, recurrence_cycle)
            VALUES ($n, $t, $s, $e, $r); SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$n", item.Name);
        cmd.Parameters.AddWithValue("$t", item.PeriodType);
        cmd.Parameters.AddWithValue("$s", item.StartDate);
        cmd.Parameters.AddWithValue("$e", item.EndDate);
        cmd.Parameters.AddWithValue("$r", item.RecurrenceCycle);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task UpdateAsync(SchedulePeriodInfo item)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE schedule_periods SET name = $n, period_type = $t, start_date = $s,
                end_date = $e, recurrence_cycle = $r
            WHERE id = $id
            """;
        cmd.Parameters.AddWithValue("$n", item.Name);
        cmd.Parameters.AddWithValue("$t", item.PeriodType);
        cmd.Parameters.AddWithValue("$s", item.StartDate);
        cmd.Parameters.AddWithValue("$e", item.EndDate);
        cmd.Parameters.AddWithValue("$r", item.RecurrenceCycle);
        cmd.Parameters.AddWithValue("$id", item.Id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM schedule_periods WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }
}
