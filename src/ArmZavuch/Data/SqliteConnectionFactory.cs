using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data;

/// <summary>
/// Создаёт подключения к основному файлу SQLite. Путь: %LocalAppData%/ArmZavuch/school.db.
/// </summary>
public sealed class SqliteConnectionFactory
{
    public string DatabasePath { get; }

    public SqliteConnectionFactory()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArmZavuch");
        Directory.CreateDirectory(folder);
        DatabasePath = Path.Combine(folder, "school.db");
    }

    public SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection($"Data Source={DatabasePath}");
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }
}
