using ArmZavuch.Models;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Repositories;

/// <summary>CRUD учителей и связей с предметами/классным руководством.</summary>
public sealed class TeacherRepository
{
    private readonly SqliteConnectionFactory _factory;

    public TeacherRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<List<Teacher>> GetAllAsync()
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, full_name, teacher_type, max_load_hours, room_id,
                   job_title, phone, contact_url, contact_note, works_with_first_grade
            FROM teachers ORDER BY full_name
            """;
        var list = new List<Teacher>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var teacher = new Teacher
            {
                Id = reader.GetInt32(0),
                FullName = reader.GetString(1),
                TeacherType = reader.GetString(2),
                MaxLoadHours = reader.GetInt32(3),
                RoomId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                JobTitle = reader.IsDBNull(5) ? null : reader.GetString(5),
                Phone = reader.IsDBNull(6) ? null : reader.GetString(6),
                ContactUrl = reader.IsDBNull(7) ? null : reader.GetString(7),
                ContactNote = reader.IsDBNull(8) ? null : reader.GetString(8),
                WorksWithFirstGrade = !reader.IsDBNull(9) && reader.GetInt32(9) != 0
            };
            await EnrichProfilesAsync(conn, teacher);
            await EnrichPreferredClassesAsync(conn, teacher);
            await EnrichCurriculumAssignmentsAsync(conn, teacher);
            list.Add(teacher);
        }
        return list;
    }

    public async Task<int> InsertAsync(Teacher item)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO teachers (full_name, teacher_type, max_load_hours, room_id,
                                  job_title, phone, contact_url, contact_note, works_with_first_grade)
            VALUES ($n, $t, $m, $r, $j, $p, $u, $c, $g); SELECT last_insert_rowid();
            """;
        BindTeacher(cmd, item);
        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        await SaveProfilesAsync(conn, id, item);
        await SavePreferredClassesAsync(conn, id, item);
        await SaveHomeroomAsync(conn, id, item.HomeroomClassId);
        return id;
    }

    public async Task UpdateAsync(Teacher item)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE teachers SET full_name=$n, teacher_type=$t, max_load_hours=$m, room_id=$r,
                job_title=$j, phone=$p, contact_url=$u, contact_note=$c, works_with_first_grade=$g
            WHERE id=$id
            """;
        BindTeacher(cmd, item);
        cmd.Parameters.AddWithValue("$id", item.Id);
        await cmd.ExecuteNonQueryAsync();

        await using var del = conn.CreateCommand();
        del.CommandText = "DELETE FROM teacher_subjects WHERE teacher_id = $id";
        del.Parameters.AddWithValue("$id", item.Id);
        await del.ExecuteNonQueryAsync();
        await SaveProfilesAsync(conn, item.Id, item);
        await SavePreferredClassesAsync(conn, item.Id, item);
        await SaveHomeroomAsync(conn, item.Id, item.HomeroomClassId);
    }

    public async Task DeleteAsync(int id)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM teachers WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Все предметы педагога (Primary/Secondary из teacher_subjects).</summary>
    public async Task<Dictionary<int, HashSet<int>>> GetSubjectIdsByTeacherAsync()
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT teacher_id, subject_id FROM teacher_subjects";
        var map = new Dictionary<int, HashSet<int>>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var tid = reader.GetInt32(0);
            var sid = reader.GetInt32(1);
            if (!map.TryGetValue(tid, out var set))
            {
                set = [];
                map[tid] = set;
            }
            set.Add(sid);
        }
        return map;
    }

    public async Task<int?> FindIdByNameAsync(string name)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM teachers WHERE full_name = $n";
        cmd.Parameters.AddWithValue("$n", name);
        var result = await cmd.ExecuteScalarAsync();
        return result is null ? null : Convert.ToInt32(result);
    }

    public async Task AddSchedulePreferredClassAsync(int teacherId, int classId)
    {
        await using var conn = _factory.CreateConnection();
        await using var ins = conn.CreateCommand();
        ins.CommandText = """
            INSERT OR IGNORE INTO teacher_preferred_classes (teacher_id, class_id, source)
            VALUES ($t, $c, $s)
            """;
        ins.Parameters.AddWithValue("$t", teacherId);
        ins.Parameters.AddWithValue("$c", classId);
        ins.Parameters.AddWithValue("$s", CurriculumAssignmentSource.Schedule);
        await ins.ExecuteNonQueryAsync();
    }

    public async Task RemoveSchedulePreferredClassAsync(int teacherId, int classId)
    {
        await using var conn = _factory.CreateConnection();
        await using var del = conn.CreateCommand();
        del.CommandText = """
            DELETE FROM teacher_preferred_classes
            WHERE teacher_id = $t AND class_id = $c AND source = $s
            """;
        del.Parameters.AddWithValue("$t", teacherId);
        del.Parameters.AddWithValue("$c", classId);
        del.Parameters.AddWithValue("$s", CurriculumAssignmentSource.Schedule);
        await del.ExecuteNonQueryAsync();
    }

    public async Task AddScheduleCurriculumAssignmentAsync(int teacherId, int curriculumId)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO teacher_curriculum_items (teacher_id, curriculum_id, source)
            VALUES ($t, $c, $s)
            """;
        cmd.Parameters.AddWithValue("$t", teacherId);
        cmd.Parameters.AddWithValue("$c", curriculumId);
        cmd.Parameters.AddWithValue("$s", CurriculumAssignmentSource.Schedule);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RemoveScheduleCurriculumAssignmentAsync(int teacherId, int curriculumId)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM teacher_curriculum_items
            WHERE teacher_id = $t AND curriculum_id = $c AND source = $s
            """;
        cmd.Parameters.AddWithValue("$t", teacherId);
        cmd.Parameters.AddWithValue("$c", curriculumId);
        cmd.Parameters.AddWithValue("$s", CurriculumAssignmentSource.Schedule);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SetCurriculumAssigneesAsync(int curriculumId, IReadOnlyList<int> teacherIds)
    {
        await using var conn = _factory.CreateConnection();
        await using var del = conn.CreateCommand();
        del.CommandText = """
            DELETE FROM teacher_curriculum_items
            WHERE curriculum_id = $c AND source = $s
            """;
        del.Parameters.AddWithValue("$c", curriculumId);
        del.Parameters.AddWithValue("$s", CurriculumAssignmentSource.Explicit);
        await del.ExecuteNonQueryAsync();

        foreach (var teacherId in teacherIds)
            await UpsertExplicitCurriculumAssignmentAsync(conn, teacherId, curriculumId);
    }

    public async Task<List<int>> GetExplicitAssigneesForCurriculumAsync(int curriculumId)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT teacher_id FROM teacher_curriculum_items
            WHERE curriculum_id = $c AND source = $s
            ORDER BY teacher_id
            """;
        cmd.Parameters.AddWithValue("$c", curriculumId);
        cmd.Parameters.AddWithValue("$s", CurriculumAssignmentSource.Explicit);
        var ids = new List<int>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            ids.Add(reader.GetInt32(0));
        return ids;
    }

    public async Task<Dictionary<int, List<int>>> GetExplicitAssigneesByCurriculumAsync()
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT curriculum_id, teacher_id
            FROM teacher_curriculum_items
            WHERE source = $s
            ORDER BY curriculum_id, teacher_id
            """;
        cmd.Parameters.AddWithValue("$s", CurriculumAssignmentSource.Explicit);
        var map = new Dictionary<int, List<int>>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var curriculumId = reader.GetInt32(0);
            var teacherId = reader.GetInt32(1);
            if (!map.TryGetValue(curriculumId, out var ids))
            {
                ids = [];
                map[curriculumId] = ids;
            }

            if (!ids.Contains(teacherId))
                ids.Add(teacherId);
        }

        return map;
    }

    public async Task RefreshCurriculumAssignmentsAsync(Teacher teacher)
    {
        await using var conn = _factory.CreateConnection();
        await EnrichCurriculumAssignmentsAsync(conn, teacher);
    }

    public async Task RefreshPreferredClassesAsync(Teacher teacher)
    {
        await using var conn = _factory.CreateConnection();
        await EnrichPreferredClassesAsync(conn, teacher);
    }

    /// <summary>Сумма часов по teacher_curriculum_items (с учётом чётности шаблона).</summary>
    public async Task<Dictionary<int, double>> GetPlannedWeeklyHoursByTeacherAsync(string templateWeekParity)
    {
        await using var conn = _factory.CreateConnection();
        return await SumCurriculumHoursAsync(conn, """
            SELECT tci.teacher_id, cu.hours_per_week, cu.week_parity
            FROM teacher_curriculum_items tci
            JOIN curriculum cu ON cu.id = tci.curriculum_id
            """, templateWeekParity);
    }

    private static async Task<Dictionary<int, double>> SumCurriculumHoursAsync(
        SqliteConnection conn,
        string sql,
        string templateWeekParity)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var map = new Dictionary<int, double>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var teacherId = reader.GetInt32(0);
            var hours = reader.GetDouble(1);
            var weekParity = reader.IsDBNull(2) ? CurriculumWeekParity.EveryWeek : reader.GetString(2);
            if (!CurriculumWeekParity.MatchesForTemplate(weekParity, templateWeekParity))
                continue;

            map[teacherId] = map.GetValueOrDefault(teacherId) + hours;
        }

        return map;
    }

    private static async Task EnrichProfilesAsync(SqliteConnection conn, Teacher teacher)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT s.name, ts.profile_type FROM teacher_subjects ts
            JOIN subjects s ON s.id = ts.subject_id
            WHERE ts.teacher_id = $id
            """;
        cmd.Parameters.AddWithValue("$id", teacher.Id);
        await using var reader = await cmd.ExecuteReaderAsync();
        var primaries = new List<string>();
        var secondaries = new List<string>();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            if (reader.GetString(1) == "Primary")
                primaries.Add(name);
            else
                secondaries.Add(name);
        }

        teacher.PrimarySubject = primaries.FirstOrDefault();
        teacher.SecondarySubjects = secondaries;

        await using var homeroom = conn.CreateCommand();
        homeroom.CommandText = """
            SELECT c.id, c.grade || c.letter FROM class_teachers ct
            JOIN school_classes c ON c.id = ct.class_id
            WHERE ct.teacher_id = $id LIMIT 1
            """;
        homeroom.Parameters.AddWithValue("$id", teacher.Id);
        await using var homeroomReader = await homeroom.ExecuteReaderAsync();
        if (await homeroomReader.ReadAsync())
        {
            teacher.HomeroomClassId = homeroomReader.GetInt32(0);
            teacher.HomeroomClass = homeroomReader.GetString(1);
        }
        else
        {
            teacher.HomeroomClassId = null;
            teacher.HomeroomClass = null;
        }
    }

    private static void BindTeacher(SqliteCommand cmd, Teacher item)
    {
        cmd.Parameters.AddWithValue("$n", item.FullName);
        cmd.Parameters.AddWithValue("$t", item.TeacherType);
        cmd.Parameters.AddWithValue("$m", item.MaxLoadHours);
        cmd.Parameters.AddWithValue("$r", (object?)item.RoomId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$j", (object?)item.JobTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$p", (object?)item.Phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$u", (object?)item.ContactUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$c", (object?)item.ContactNote ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$g", item.WorksWithFirstGrade ? 1 : 0);
    }

    private static async Task EnrichPreferredClassesAsync(SqliteConnection conn, Teacher teacher)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT tpc.class_id, c.grade || c.letter
            FROM teacher_preferred_classes tpc
            JOIN school_classes c ON c.id = tpc.class_id
            WHERE tpc.teacher_id = $id
            ORDER BY c.grade, c.letter
            """;
        cmd.Parameters.AddWithValue("$id", teacher.Id);
        var ids = new List<int>();
        var names = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetInt32(0));
            names.Add(reader.GetString(1));
        }
        teacher.PreferredClassIds = ids;
        teacher.PreferredClassesDisplay = string.Join(", ", names);
    }

    private static async Task EnrichCurriculumAssignmentsAsync(SqliteConnection conn, Teacher teacher)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT tci.curriculum_id, cu.class_id, c.grade || c.letter, cu.subject_id, s.name,
                   cu.week_parity, cu.hours_per_week
            FROM teacher_curriculum_items tci
            JOIN curriculum cu ON cu.id = tci.curriculum_id
            JOIN school_classes c ON c.id = cu.class_id
            JOIN subjects s ON s.id = cu.subject_id
            WHERE tci.teacher_id = $id
            ORDER BY c.grade, c.letter, s.name, cu.week_parity
            """;
        cmd.Parameters.AddWithValue("$id", teacher.Id);
        var list = new List<TeacherCurriculumAssignment>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new TeacherCurriculumAssignment
            {
                CurriculumId = reader.GetInt32(0),
                ClassId = reader.GetInt32(1),
                ClassName = reader.GetString(2),
                SubjectId = reader.GetInt32(3),
                SubjectName = reader.GetString(4),
                WeekParity = reader.IsDBNull(5) ? CurriculumWeekParity.EveryWeek : reader.GetString(5),
                HoursPerWeek = reader.GetDouble(6)
            });
        }

        teacher.CurriculumAssignments = list;
    }

    private static async Task SavePreferredClassesAsync(SqliteConnection conn, int teacherId, Teacher item)
    {
        await using var del = conn.CreateCommand();
        del.CommandText = """
            DELETE FROM teacher_preferred_classes
            WHERE teacher_id = $id AND source = $s
            """;
        del.Parameters.AddWithValue("$id", teacherId);
        del.Parameters.AddWithValue("$s", CurriculumAssignmentSource.Explicit);
        await del.ExecuteNonQueryAsync();

        foreach (var classId in item.PreferredClassIds.Distinct())
            await UpsertExplicitPreferredClassAsync(conn, teacherId, classId);
    }

    private static async Task UpsertExplicitPreferredClassAsync(
        SqliteConnection conn, int teacherId, int classId)
    {
        await using var ins = conn.CreateCommand();
        ins.CommandText = """
            INSERT INTO teacher_preferred_classes (teacher_id, class_id, source)
            VALUES ($t, $c, $s)
            ON CONFLICT(teacher_id, class_id) DO UPDATE SET source = $s
            """;
        ins.Parameters.AddWithValue("$t", teacherId);
        ins.Parameters.AddWithValue("$c", classId);
        ins.Parameters.AddWithValue("$s", CurriculumAssignmentSource.Explicit);
        await ins.ExecuteNonQueryAsync();
    }

    private static async Task SaveHomeroomAsync(SqliteConnection conn, int teacherId, int? classId)
    {
        await using (var clearTeacher = conn.CreateCommand())
        {
            clearTeacher.CommandText = "DELETE FROM class_teachers WHERE teacher_id = $t";
            clearTeacher.Parameters.AddWithValue("$t", teacherId);
            await clearTeacher.ExecuteNonQueryAsync();
        }

        if (classId is not int cid)
            return;

        await using (var clearClass = conn.CreateCommand())
        {
            clearClass.CommandText = "DELETE FROM class_teachers WHERE class_id = $c";
            clearClass.Parameters.AddWithValue("$c", cid);
            await clearClass.ExecuteNonQueryAsync();
        }

        await using var ins = conn.CreateCommand();
        ins.CommandText = """
            INSERT INTO class_teachers (teacher_id, class_id)
            VALUES ($t, $c)
            """;
        ins.Parameters.AddWithValue("$t", teacherId);
        ins.Parameters.AddWithValue("$c", cid);
        await ins.ExecuteNonQueryAsync();
    }

    private static async Task SaveProfilesAsync(SqliteConnection conn, int teacherId, Teacher item)
    {
        if (!string.IsNullOrWhiteSpace(item.PrimarySubject))
        {
            var primaryId = await ResolveSubjectIdAsync(conn, item.PrimarySubject);
            if (primaryId is int pid)
                await InsertTeacherSubjectAsync(conn, teacherId, pid, "Primary");
        }

        foreach (var name in item.SecondarySubjects
                     .Where(n => !string.IsNullOrWhiteSpace(n))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (name.Equals(item.PrimarySubject, StringComparison.OrdinalIgnoreCase))
                continue;

            var subjectId = await ResolveSubjectIdAsync(conn, name);
            if (subjectId is int sid)
                await InsertTeacherSubjectAsync(conn, teacherId, sid, "Secondary");
        }
    }

    private static async Task<int?> ResolveSubjectIdAsync(SqliteConnection conn, string name)
    {
        await using var subjectCmd = conn.CreateCommand();
        subjectCmd.CommandText = "SELECT id FROM subjects WHERE name = $n COLLATE NOCASE";
        subjectCmd.Parameters.AddWithValue("$n", name.Trim());
        var subjectId = await subjectCmd.ExecuteScalarAsync();
        return subjectId is null ? null : Convert.ToInt32(subjectId);
    }

    private static async Task InsertTeacherSubjectAsync(
        SqliteConnection conn, int teacherId, int subjectId, string profileType)
    {
        await using var link = conn.CreateCommand();
        link.CommandText = """
            INSERT OR IGNORE INTO teacher_subjects (teacher_id, subject_id, profile_type)
            VALUES ($t, $s, $p)
            """;
        link.Parameters.AddWithValue("$t", teacherId);
        link.Parameters.AddWithValue("$s", subjectId);
        link.Parameters.AddWithValue("$p", profileType);
        await link.ExecuteNonQueryAsync();
    }

    private static async Task UpsertExplicitCurriculumAssignmentAsync(
        SqliteConnection conn, int teacherId, int curriculumId)
    {
        await using var ins = conn.CreateCommand();
        ins.CommandText = """
            INSERT INTO teacher_curriculum_items (teacher_id, curriculum_id, source)
            VALUES ($t, $c, $s)
            ON CONFLICT(teacher_id, curriculum_id) DO UPDATE SET source = $s
            """;
        ins.Parameters.AddWithValue("$t", teacherId);
        ins.Parameters.AddWithValue("$c", curriculumId);
        ins.Parameters.AddWithValue("$s", CurriculumAssignmentSource.Explicit);
        await ins.ExecuteNonQueryAsync();
    }
}
