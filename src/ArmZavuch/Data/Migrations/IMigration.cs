using ArmZavuch.Data;

namespace ArmZavuch.Data.Migrations;

/// <summary>
/// Одна версионированная миграция схемы БД.
/// </summary>
public interface IMigration
{
    int Version { get; }
    Task ApplyAsync(SqliteConnectionFactory factory);
}
