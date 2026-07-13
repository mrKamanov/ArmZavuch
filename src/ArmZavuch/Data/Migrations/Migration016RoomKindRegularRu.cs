using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Убирает английское значение Regular у типа кабинета.</summary>
public sealed class Migration016RoomKindRegularRu : IMigration
{
    public int Version => 16;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE rooms SET room_kind = ''
            WHERE room_kind = 'Regular' OR lower(trim(room_kind)) = 'regular';
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}
