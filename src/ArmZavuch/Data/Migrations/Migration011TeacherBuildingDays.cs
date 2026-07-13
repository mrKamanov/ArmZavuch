using Microsoft.Data.Sqlite;
using ArmZavuch.Models;

namespace ArmZavuch.Data.Migrations;

/// <summary>Привязка педагога к зданию по дням недели.</summary>
public sealed class Migration011TeacherBuildingDays : IMigration
{
    public int Version => 11;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS teacher_building_days (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                teacher_id INTEGER NOT NULL REFERENCES teachers(id) ON DELETE CASCADE,
                day_of_week INTEGER NOT NULL CHECK(day_of_week BETWEEN 1 AND 6),
                building_id INTEGER NOT NULL REFERENCES buildings(id) ON DELETE CASCADE,
                UNIQUE (teacher_id, day_of_week)
            );
            CREATE INDEX IF NOT EXISTS idx_teacher_building_days_teacher
                ON teacher_building_days (teacher_id);
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}
