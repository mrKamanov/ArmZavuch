using ArmZavuch.Models;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Repositories;

/// <summary>CRUD предметов; удаление с проверкой расписания и очисткой нагрузки.</summary>
public sealed class SubjectRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SubjectRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<List<Subject>> GetAllAsync()
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, difficulty_score FROM subjects ORDER BY name";
        var list = new List<Subject>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Subject
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                DifficultyScore = reader.GetDouble(2)
            });
        }
        return list;
    }

    public async Task<int> InsertAsync(Subject item)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO subjects (name, difficulty_score) VALUES ($n, $d); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$n", item.Name);
        cmd.Parameters.AddWithValue("$d", item.DifficultyScore);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task UpdateAsync(Subject item)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE subjects SET name = $n, difficulty_score = $d WHERE id = $id";
        cmd.Parameters.AddWithValue("$n", item.Name);
        cmd.Parameters.AddWithValue("$d", item.DifficultyScore);
        cmd.Parameters.AddWithValue("$id", item.Id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var result = await TryDeleteAsync(id);
        if (!result.Success)
            throw new InvalidOperationException(result.ErrorMessage);
    }

    /// <summary>Удаляет предмет и связанную нагрузку/профили, если предмет не стоит в недельном расписании.</summary>
    public async Task<BuildingDeleteResult> TryDeleteAsync(int subjectId)
    {
        await using var conn = _factory.CreateConnection();
        await using var nameCmd = conn.CreateCommand();
        nameCmd.CommandText = "SELECT name FROM subjects WHERE id = $id";
        nameCmd.Parameters.AddWithValue("$id", subjectId);
        var nameObj = await nameCmd.ExecuteScalarAsync();
        if (nameObj is null)
            return BuildingDeleteResult.Fail("Предмет не найден.");
        var subjectName = (string)nameObj;

        var scheduleSlots = await CountAsync(conn,
            "SELECT COUNT(*) FROM week_template_slots WHERE subject_id = $id",
            subjectId);
        if (scheduleSlots > 0)
        {
            return BuildingDeleteResult.Fail(
                $"Предмет «{subjectName}» нельзя удалить: он указан в недельном расписании ({scheduleSlots} урок(ов)). " +
                "Сначала удалите или измените эти уроки в конструкторе.");
        }

        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync();
        try
        {
            await ExecAsync(conn, tx,
                """
                DELETE FROM teacher_curriculum_items
                WHERE curriculum_id IN (SELECT id FROM curriculum WHERE subject_id = $id)
                """,
                subjectId);
            await ExecAsync(conn, tx, "DELETE FROM curriculum WHERE subject_id = $id", subjectId);
            await ExecAsync(conn, tx, "DELETE FROM teacher_subjects WHERE subject_id = $id", subjectId);
            await ExecAsync(conn, tx, "DELETE FROM teacher_class_subjects WHERE subject_id = $id", subjectId);
            await ExecAsync(conn, tx, "DELETE FROM subjects WHERE id = $id", subjectId);
            await tx.CommitAsync();
            return BuildingDeleteResult.Ok();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private static async Task<int> CountAsync(SqliteConnection conn, string sql, int subjectId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$id", subjectId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task ExecAsync(SqliteConnection conn, SqliteTransaction tx, string sql, int subjectId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$id", subjectId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int?> FindIdByNameAsync(string name)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM subjects WHERE lower(name) = lower($n) LIMIT 1";
        cmd.Parameters.AddWithValue("$n", name.Trim());
        var result = await cmd.ExecuteScalarAsync();
        return result is null ? null : Convert.ToInt32(result);
    }

    public async Task<Subject?> GetByIdAsync(int id)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, difficulty_score FROM subjects WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;
        return new Subject
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            DifficultyScore = reader.GetDouble(2)
        };
    }
}
