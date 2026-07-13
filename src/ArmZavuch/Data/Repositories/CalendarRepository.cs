using ArmZavuch.Models;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Repositories;

/// <summary>CRUD учебного календаря (каникулы, праздники, компенсации).</summary>
public sealed class CalendarRepository
{
    private readonly SqliteConnectionFactory _factory;

    public CalendarRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<List<CalendarEntry>> GetAllAsync()
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, start_date, end_date, exception_type, donor_day_of_week, note
            FROM calendar_exceptions ORDER BY start_date
            """;
        var list = new List<CalendarEntry>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new CalendarEntry
            {
                Id = reader.GetInt32(0),
                StartDate = reader.GetString(1),
                EndDate = reader.IsDBNull(2) ? null : reader.GetString(2),
                ExceptionType = reader.GetString(3),
                DonorDayOfWeek = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                Note = reader.IsDBNull(5) ? null : reader.GetString(5)
            });
        }
        return list;
    }

    public async Task<int> InsertAsync(CalendarEntry entry)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO calendar_exceptions (start_date, end_date, exception_type, donor_day_of_week, note)
            VALUES ($s, $e, $t, $d, $n);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$s", entry.StartDate);
        cmd.Parameters.AddWithValue("$e", (object?)entry.EndDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$t", entry.ExceptionType);
        cmd.Parameters.AddWithValue("$d", (object?)entry.DonorDayOfWeek ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$n", (object?)entry.Note ?? DBNull.Value);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task UpdateAsync(CalendarEntry entry)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE calendar_exceptions SET start_date = $s, end_date = $e, exception_type = $t,
                donor_day_of_week = $d, note = $n
            WHERE id = $id
            """;
        cmd.Parameters.AddWithValue("$s", entry.StartDate);
        cmd.Parameters.AddWithValue("$e", (object?)entry.EndDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$t", entry.ExceptionType);
        cmd.Parameters.AddWithValue("$d", (object?)entry.DonorDayOfWeek ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$n", (object?)entry.Note ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", entry.Id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM calendar_exceptions WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }
}
