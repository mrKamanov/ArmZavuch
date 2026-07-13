using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Удаляет неиспользуемый цикл «Custom» — заменяет на «EveryWeek».</summary>
public sealed class Migration032RemoveCustomRecurrenceCycle : IMigration
{
    public int Version => 32;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE schedule_periods
            SET recurrence_cycle = 'EveryWeek'
            WHERE recurrence_cycle = 'Custom'
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}
