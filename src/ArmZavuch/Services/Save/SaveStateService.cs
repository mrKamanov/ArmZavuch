using ArmZavuch.Data;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Services.Save;

/// <summary>
/// Фиксирует момент последнего явного сохранения в meta.last_saved_at.
/// </summary>
public sealed class SaveStateService : ISaveStateService
{
    private readonly SqliteConnectionFactory _factory;

    public bool IsDirty { get; private set; }
    public DateTime? LastSavedAt { get; private set; }
    public event Action? DirtyStateChanged;

    public SaveStateService(SqliteConnectionFactory factory)
    {
        _factory = factory;
        LastSavedAt = LoadLastSavedAt();
    }

    public void MarkDirty()
    {
        if (IsDirty)
            return;

        IsDirty = true;
        DirtyStateChanged?.Invoke();
    }

    public async Task SaveAsync()
    {
        var now = DateTime.Now;
        await using var connection = _factory.CreateConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO meta (key, value) VALUES ('last_saved_at', $v)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        cmd.Parameters.AddWithValue("$v", now.ToString("O"));
        await cmd.ExecuteNonQueryAsync();

        var recoveryPath = Path.Combine(
            Path.GetDirectoryName(_factory.DatabasePath)!,
            "recovery.db");
        if (File.Exists(recoveryPath))
            File.Delete(recoveryPath);

        LastSavedAt = now;
        IsDirty = false;
        DirtyStateChanged?.Invoke();
    }

    private DateTime? LoadLastSavedAt()
    {
        try
        {
            using var connection = _factory.CreateConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT value FROM meta WHERE key = 'last_saved_at'";
            var result = cmd.ExecuteScalar();
            return result is string s ? DateTime.Parse(s) : null;
        }
        catch (SqliteException)
        {
            return null;
        }
    }
}
