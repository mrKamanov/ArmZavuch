using ArmZavuch.Data;
using ArmZavuch.Models;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Services.Data;

/// <summary>Выборочная загрузка разделов из архивной БД в текущую с merge/replace.</summary>
public sealed class AppDataSelectiveImporter
{
    private const string SourceAlias = "srcdb";

    private readonly SqliteConnectionFactory _factory;

    public AppDataSelectiveImporter(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<AppDataTransferResult> ImportAsync(
        string sourceDbPath,
        IReadOnlyList<AppDataTransferSection> sections,
        AppDataImportMode mode)
    {
        if (sections.Count == 0)
            return AppDataTransferResult.Fail("Не выбран ни один раздел для загрузки.");

        var ordered = AppDataSectionCatalog.ImportOrder.Where(sections.Contains).ToList();
        var context = new ImportContext(mode);

        SqliteConnection.ClearAllPools();

        await using var conn = _factory.CreateConnection();
        await ExecuteAsync(conn, null, "PRAGMA busy_timeout = 60000");
        await AttachSourceAsync(conn, sourceDbPath);

        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync();
        await ExecuteAsync(conn, tx, "PRAGMA foreign_keys = OFF");

        try
        {
            foreach (var section in ordered)
                await ImportSectionAsync(conn, tx, section, context);

            await ExecuteAsync(conn, tx, "PRAGMA foreign_keys = ON");
            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            try
            {
                await tx.RollbackAsync();
            }
            catch
            {
                // rollback best-effort
            }

            return AppDataTransferResult.Fail($"Ошибка при загрузке раздела: {ex.Message}");
        }
        finally
        {
            await DetachSourceAsync(conn);
        }

        var summary = BuildSummary(context, ordered, mode);
        return AppDataTransferResult.Ok(summary);
    }

    private static string BuildSummary(ImportContext ctx, IReadOnlyList<AppDataTransferSection> sections, AppDataImportMode mode)
    {
        var modeLabel = mode == AppDataImportMode.Merge ? "дополнение/обновление" : "замена";
        var names = string.Join(", ", sections.Select(AppDataSectionCatalog.Title));
        var lines = new List<string> { $"Загружены разделы ({modeLabel}): {names}." };
        if (ctx.Stats.Count > 0)
            lines.AddRange(ctx.Stats.Select(kv => $"• {AppDataSectionCatalog.Title(kv.Key)}: {kv.Value}"));
        if (ctx.Warnings.Count > 0)
            lines.AddRange(ctx.Warnings.Take(5).Select(w => $"⚠ {w}"));
        return string.Join("\n", lines);
    }

    private async Task ImportSectionAsync(
        SqliteConnection conn,
        SqliteTransaction tx,
        AppDataTransferSection section,
        ImportContext ctx)
    {
        switch (section)
        {
            case AppDataTransferSection.Buildings:
                await ImportBuildingsAsync(conn, tx, ctx);
                break;
            case AppDataTransferSection.Subjects:
                await ImportSubjectsAsync(conn, tx, ctx);
                break;
            case AppDataTransferSection.Teachers:
                await ImportTeachersAsync(conn, tx, ctx);
                break;
            case AppDataTransferSection.Rooms:
                await ImportRoomsAsync(conn, tx, ctx);
                break;
            case AppDataTransferSection.Classes:
                await ImportClassesAsync(conn, tx, ctx);
                break;
            case AppDataTransferSection.Curriculum:
                await ImportCurriculumAsync(conn, tx, ctx);
                break;
            case AppDataTransferSection.Bells:
                await ImportBellsAsync(conn, tx, ctx);
                break;
            case AppDataTransferSection.Schedule:
                await ImportScheduleAsync(conn, tx, ctx);
                break;
            case AppDataTransferSection.Calendar:
                await ImportCalendarAsync(conn, tx, ctx);
                break;
            case AppDataTransferSection.DayOperations:
                await ImportDayOperationsAsync(conn, tx, ctx);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(section), section, null);
        }
    }

    private async Task ImportBuildingsAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        if (ctx.Mode == AppDataImportMode.Replace)
        {
            await ExecuteAsync(conn, tx, "DELETE FROM building_routes");
            if (await TableHasRowsAsync(conn, tx, "rooms"))
            {
                ctx.Warnings.Add(
                    "Замена зданий: кабинеты сохранены; совпадение по названию здания обновляет цвет.");
            }
        }

        var rows = await ReadSourceBuildingsAsync(conn, tx);
        var count = 0;
        foreach (var row in rows)
        {
            var targetId = await FindBuildingIdByNameAsync(conn, tx, row.Name);
            if (targetId is int existing)
            {
                await ExecuteAsync(conn, tx,
                    "UPDATE buildings SET color_hex = $c WHERE id = $id",
                    ("$c", row.ColorHex), ("$id", existing));
                ctx.BuildingIds[row.SourceId] = existing;
            }
            else
            {
                var newId = await InsertScalarAsync(conn, tx,
                    "INSERT INTO buildings (name, color_hex) VALUES ($n, $c); SELECT last_insert_rowid();",
                    ("$n", row.Name), ("$c", row.ColorHex));
                ctx.BuildingIds[row.SourceId] = newId;
            }

            count++;
        }

        if (ctx.Mode == AppDataImportMode.Replace)
            await ExecuteAsync(conn, tx, "DELETE FROM building_routes");

        var routes = await ReadSourceRoutesAsync(conn, tx);
        foreach (var route in routes)
        {
            if (!ctx.BuildingIds.TryGetValue(route.FromId, out var fromId)
                || !ctx.BuildingIds.TryGetValue(route.ToId, out var toId))
                continue;

            await ExecuteAsync(conn, tx, """
                INSERT INTO building_routes (from_building_id, to_building_id, minutes)
                VALUES ($f, $t, $m)
                ON CONFLICT(from_building_id, to_building_id) DO UPDATE SET minutes = excluded.minutes
                """,
                ("$f", fromId), ("$t", toId), ("$m", route.Minutes));
        }

        ctx.Stats[AppDataTransferSection.Buildings] = $"{count} зданий, {routes.Count} переходов";
    }

    private async Task ImportSubjectsAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        if (ctx.Mode == AppDataImportMode.Replace)
        {
            await ExecuteAsync(conn, tx, "DELETE FROM curriculum");
            await ExecuteAsync(conn, tx, "DELETE FROM teacher_subjects");
            await ExecuteAsync(conn, tx, "DELETE FROM subjects");
            ctx.Warnings.Add("Замена предметов: нагрузка и профили учителей по предметам будут перезагружены при импорте этих разделов.");
        }

        var rows = await ReadSourceSubjectsAsync(conn, tx);
        var count = 0;
        foreach (var row in rows)
        {
            var targetId = await FindSubjectIdByNameAsync(conn, tx, row.Name);
            if (targetId is int existing)
            {
                await ExecuteAsync(conn, tx,
                    "UPDATE subjects SET difficulty_score = $d WHERE id = $id",
                    ("$d", row.DifficultyScore), ("$id", existing));
                ctx.SubjectIds[row.SourceId] = existing;
            }
            else
            {
                var newId = await InsertScalarAsync(conn, tx,
                    "INSERT INTO subjects (name, difficulty_score) VALUES ($n, $d); SELECT last_insert_rowid();",
                    ("$n", row.Name), ("$d", row.DifficultyScore));
                ctx.SubjectIds[row.SourceId] = newId;
            }

            count++;
        }

        ctx.Stats[AppDataTransferSection.Subjects] = $"{count} предметов";
    }

    private async Task ImportTeachersAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        var slotTeacherNames = await SnapshotSlotTeacherNamesAsync(conn, tx);

        if (ctx.Mode == AppDataImportMode.Replace)
            await ClearTeacherTablesAsync(conn, tx, clearSlots: false);

        var rows = await ReadSourceTeachersAsync(conn, tx);
        var count = 0;
        foreach (var row in rows)
        {
            var targetId = await FindTeacherIdByNameAsync(conn, tx, row.FullName);
            if (targetId is int existing)
            {
                await UpdateTeacherAsync(conn, tx, existing, row);
                ctx.TeacherIds[row.SourceId] = existing;
            }
            else
            {
                var newId = await InsertTeacherAsync(conn, tx, row);
                ctx.TeacherIds[row.SourceId] = newId;
            }

            count++;
        }

        await ImportTeacherChildTablesAsync(conn, tx, ctx);
        await RemapSlotTeachersByNameAsync(conn, tx, slotTeacherNames);

        ctx.Stats[AppDataTransferSection.Teachers] = $"{count} сотрудников";
    }

    private async Task ImportRoomsAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        if (ctx.Mode == AppDataImportMode.Replace)
        {
            await ExecuteAsync(conn, tx, "DELETE FROM week_template_slots");
            await ExecuteAsync(conn, tx, "DELETE FROM day_overrides WHERE room_id IS NOT NULL");
            await ExecuteAsync(conn, tx, "UPDATE teachers SET room_id = NULL WHERE room_id IS NOT NULL");
            await ExecuteAsync(conn, tx, "UPDATE school_classes SET default_room_id = NULL, default_pe_room_id = NULL");
            await ExecuteAsync(conn, tx, "DELETE FROM rooms");
        }

        await EnsureBuildingMapFromNamesAsync(conn, tx, ctx);

        var rows = await ReadSourceRoomsAsync(conn, tx);
        var count = 0;
        foreach (var row in rows)
        {
            if (!ctx.BuildingIds.TryGetValue(row.BuildingId, out var buildingId))
                continue;

            int? assignedTeacherId = null;
            if (row.AssignedTeacherId is int srcTeacher
                && ctx.TeacherIds.TryGetValue(srcTeacher, out var mappedTeacher))
                assignedTeacherId = mappedTeacher;

            var targetId = await FindRoomIdAsync(conn, tx, row.Number, buildingId);
            if (targetId is int existing)
            {
                await ExecuteAsync(conn, tx, """
                    UPDATE rooms SET capacity = $c, room_kind = $k, assigned_teacher_id = $t
                    WHERE id = $id
                    """,
                    ("$c", row.Capacity), ("$k", row.RoomKind), ("$t", (object?)assignedTeacherId ?? DBNull.Value),
                    ("$id", existing));
                ctx.RoomIds[row.SourceId] = existing;
            }
            else
            {
                var newId = await InsertScalarAsync(conn, tx, """
                    INSERT INTO rooms (number, building_id, capacity, room_kind, assigned_teacher_id)
                    VALUES ($n, $b, $c, $k, $t); SELECT last_insert_rowid();
                    """,
                    ("$n", row.Number), ("$b", buildingId), ("$c", row.Capacity), ("$k", row.RoomKind),
                    ("$t", (object?)assignedTeacherId ?? DBNull.Value));
                ctx.RoomIds[row.SourceId] = newId;
            }

            count++;
        }

        ctx.Stats[AppDataTransferSection.Rooms] = $"{count} кабинетов";
    }

    private async Task ImportClassesAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        if (ctx.Mode == AppDataImportMode.Replace)
        {
            await ExecuteAsync(conn, tx, "DELETE FROM week_template_slots");
            await ExecuteAsync(conn, tx, "DELETE FROM curriculum");
            await ExecuteAsync(conn, tx, "DELETE FROM class_teachers");
            await ExecuteAsync(conn, tx, "DELETE FROM teacher_preferred_classes");
            await ExecuteAsync(conn, tx, "DELETE FROM school_classes");
        }

        await EnsureBuildingMapFromNamesAsync(conn, tx, ctx);
        await EnsureRoomMapFromKeysAsync(conn, tx, ctx);

        var rows = await ReadSourceClassesAsync(conn, tx);
        var count = 0;
        foreach (var row in rows)
        {
            int? buildingId = row.BuildingId is int b && ctx.BuildingIds.TryGetValue(b, out var bid) ? bid : null;
            int? defaultRoomId = row.DefaultRoomId is int dr && ctx.RoomIds.TryGetValue(dr, out var drid) ? drid : null;
            int? peRoomId = row.DefaultPeRoomId is int pr && ctx.RoomIds.TryGetValue(pr, out var prid) ? prid : null;
            int? bellTemplateId = row.BellTemplateId is int bt && ctx.BellTemplateIds.TryGetValue(bt, out var btid) ? btid : null;

            var targetId = await FindClassIdAsync(conn, tx, row.Grade, row.Letter, row.Shift);
            if (targetId is int existing)
            {
                await ExecuteAsync(conn, tx, """
                    UPDATE school_classes
                    SET student_count = $s, is_correctional = $ic, building_id = $b,
                        default_room_id = $dr, default_pe_room_id = $pr, bell_template_id = $bt
                    WHERE id = $id
                    """,
                    ("$s", row.StudentCount), ("$ic", row.IsCorrectional ? 1 : 0),
                    ("$b", (object?)buildingId ?? DBNull.Value),
                    ("$dr", (object?)defaultRoomId ?? DBNull.Value),
                    ("$pr", (object?)peRoomId ?? DBNull.Value),
                    ("$bt", (object?)bellTemplateId ?? DBNull.Value),
                    ("$id", existing));
                ctx.ClassIds[row.SourceId] = existing;
            }
            else
            {
                var newId = await InsertScalarAsync(conn, tx, """
                    INSERT INTO school_classes
                    (grade, letter, shift, student_count, is_correctional, building_id, default_room_id, default_pe_room_id, bell_template_id)
                    VALUES ($g, $l, $sh, $s, $ic, $b, $dr, $pr, $bt); SELECT last_insert_rowid();
                    """,
                    ("$g", row.Grade), ("$l", row.Letter), ("$sh", row.Shift), ("$s", row.StudentCount),
                    ("$ic", row.IsCorrectional ? 1 : 0),
                    ("$b", (object?)buildingId ?? DBNull.Value),
                    ("$dr", (object?)defaultRoomId ?? DBNull.Value),
                    ("$pr", (object?)peRoomId ?? DBNull.Value),
                    ("$bt", (object?)bellTemplateId ?? DBNull.Value));
                ctx.ClassIds[row.SourceId] = newId;
            }

            count++;
        }

        await ImportClassTeachersAsync(conn, tx, ctx);
        ctx.Stats[AppDataTransferSection.Classes] = $"{count} классов";
    }

    private async Task ImportCurriculumAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        await EnsureClassMapFromKeysAsync(conn, tx, ctx);
        await EnsureSubjectMapFromNamesAsync(conn, tx, ctx);

        if (ctx.Mode == AppDataImportMode.Replace)
        {
            await ExecuteAsync(conn, tx, "DELETE FROM curriculum_template_items");
            await ExecuteAsync(conn, tx, "DELETE FROM curriculum_templates");
            await ExecuteAsync(conn, tx, "DELETE FROM curriculum");
        }

        var templates = await ReadSourceCurriculumTemplatesAsync(conn, tx);
        foreach (var template in templates)
        {
            var targetTemplateId = await FindCurriculumTemplateIdAsync(conn, tx, template.Name);
            if (targetTemplateId is null)
            {
                targetTemplateId = await InsertScalarAsync(conn, tx, """
                    INSERT INTO curriculum_templates (name, grade_from, grade_to, is_builtin, sort_order)
                    VALUES ($n, $gf, $gt, $b, $so); SELECT last_insert_rowid();
                    """,
                    ("$n", template.Name), ("$gf", template.GradeFrom), ("$gt", template.GradeTo),
                    ("$b", template.IsBuiltIn ? 1 : 0), ("$so", template.SortOrder));
            }

            ctx.CurriculumTemplateIds[template.SourceId] = targetTemplateId.Value;
        }

        var templateItems = await ReadSourceCurriculumTemplateItemsAsync(conn, tx);
        foreach (var item in templateItems)
        {
            if (!ctx.CurriculumTemplateIds.TryGetValue(item.TemplateId, out var templateId))
                continue;

            await ExecuteAsync(conn, tx, """
                INSERT INTO curriculum_template_items
                (template_id, subject_name, hours_per_week, difficulty_score, has_subgroups, week_parity, item_grade_from, item_grade_to)
                VALUES ($t, $s, $h, $d, $g, $p, $gf, $gt)
                """,
                ("$t", templateId), ("$s", item.SubjectName), ("$h", item.HoursPerWeek),
                ("$d", item.DifficultyScore), ("$g", item.HasSubgroups ? 1 : 0),
                ("$p", item.WeekParity), ("$gf", item.ItemGradeFrom), ("$gt", item.ItemGradeTo));
        }

        if (ctx.Mode == AppDataImportMode.Replace)
            await ExecuteAsync(conn, tx, "DELETE FROM curriculum");

        var rows = await ReadSourceCurriculumAsync(conn, tx);
        var count = 0;
        foreach (var row in rows)
        {
            if (!ctx.ClassIds.TryGetValue(row.ClassId, out var classId)
                || !ctx.SubjectIds.TryGetValue(row.SubjectId, out var subjectId))
                continue;

            await ExecuteAsync(conn, tx, """
                INSERT INTO curriculum (class_id, subject_id, hours_per_week, has_subgroups, week_parity)
                VALUES ($c, $s, $h, $g, $p)
                ON CONFLICT(class_id, subject_id, week_parity) DO UPDATE SET
                    hours_per_week = excluded.hours_per_week,
                    has_subgroups = excluded.has_subgroups
                """,
                ("$c", classId), ("$s", subjectId), ("$h", row.HoursPerWeek),
                ("$g", row.HasSubgroups ? 1 : 0), ("$p", row.WeekParity));
            count++;
        }

        ctx.Stats[AppDataTransferSection.Curriculum] = $"{count} строк нагрузки";
    }

    private async Task ImportBellsAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        if (ctx.Mode == AppDataImportMode.Replace)
        {
            await ExecuteAsync(conn, tx, "UPDATE day_overrides SET bell_template_id = NULL WHERE bell_template_id IS NOT NULL");
            await ExecuteAsync(conn, tx, "UPDATE school_classes SET bell_template_id = NULL WHERE bell_template_id IS NOT NULL");
            await ExecuteAsync(conn, tx, "DELETE FROM bell_periods");
            await ExecuteAsync(conn, tx, "DELETE FROM bell_templates");
        }

        var templates = await ReadSourceBellTemplatesAsync(conn, tx);
        foreach (var template in templates)
        {
            var targetId = await FindBellTemplateIdByNameAsync(conn, tx, template.Name);
            if (targetId is null)
            {
                targetId = await InsertScalarAsync(conn, tx,
                    "INSERT INTO bell_templates (name) VALUES ($n); SELECT last_insert_rowid();",
                    ("$n", template.Name));
            }

            ctx.BellTemplateIds[template.SourceId] = targetId.Value;
        }

        if (ctx.Mode == AppDataImportMode.Replace)
            await ExecuteAsync(conn, tx, "DELETE FROM bell_periods");

        var periods = await ReadSourceBellPeriodsAsync(conn, tx);
        foreach (var period in periods)
        {
            if (!ctx.BellTemplateIds.TryGetValue(period.TemplateId, out var templateId))
                continue;

            await ExecuteAsync(conn, tx, """
                INSERT INTO bell_periods (template_id, lesson_number, shift, start_time, end_time, period_kind)
                VALUES ($t, $l, $sh, $st, $en, $k)
                ON CONFLICT(template_id, lesson_number, shift, period_kind) DO UPDATE SET
                    start_time = excluded.start_time,
                    end_time = excluded.end_time
                """,
                ("$t", templateId), ("$l", period.LessonNumber), ("$sh", period.Shift),
                ("$st", period.StartTime), ("$en", period.EndTime), ("$k", period.PeriodKind));
        }

        ctx.Stats[AppDataTransferSection.Bells] = $"{templates.Count} шаблонов, {periods.Count} периодов";
    }

    private async Task ImportScheduleAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        await EnsureClassMapFromKeysAsync(conn, tx, ctx);
        await EnsureSubjectMapFromNamesAsync(conn, tx, ctx);
        await EnsureTeacherMapFromNamesAsync(conn, tx, ctx);
        await EnsureRoomMapFromKeysAsync(conn, tx, ctx);

        if (ctx.Mode == AppDataImportMode.Replace)
        {
            await ExecuteAsync(conn, tx, "DELETE FROM week_template_auto_snapshots");
            await ExecuteAsync(conn, tx, "DELETE FROM week_template_slots");
            await ExecuteAsync(conn, tx, "DELETE FROM week_templates");
        }

        var templates = await ReadSourceWeekTemplatesAsync(conn, tx);
        foreach (var template in templates)
        {
            var targetId = await FindWeekTemplateIdByNameAsync(conn, tx, template.Name);
            if (targetId is null)
            {
                targetId = await InsertScalarAsync(conn, tx,
                    "INSERT INTO week_templates (name, copied_from_id) VALUES ($n, NULL); SELECT last_insert_rowid();",
                    ("$n", template.Name));
            }

            ctx.WeekTemplateIds[template.SourceId] = targetId.Value;
        }

        if (ctx.Mode == AppDataImportMode.Replace)
            await ExecuteAsync(conn, tx, "DELETE FROM week_template_slots");

        var slots = await ReadSourceSlotsAsync(conn, tx);
        var count = 0;
        foreach (var slot in slots)
        {
            if (!ctx.WeekTemplateIds.TryGetValue(slot.WeekTemplateId, out var weekTemplateId)
                || !ctx.ClassIds.TryGetValue(slot.ClassId, out var classId)
                || !ctx.SubjectIds.TryGetValue(slot.SubjectId, out var subjectId)
                || !ctx.TeacherIds.TryGetValue(slot.TeacherId, out var teacherId)
                || !ctx.RoomIds.TryGetValue(slot.RoomId, out var roomId))
                continue;

            await ExecuteAsync(conn, tx, """
                INSERT INTO week_template_slots
                (week_template_id, day_of_week, lesson_number, class_id, subject_id, teacher_id, room_id, subgroup_index, week_parity, bell_template_name, is_partial, partial_kind, partial_minutes)
                VALUES ($w, $d, $l, $c, $s, $t, $r, $g, $p, $bn, $ip, $pk, $pm)
                """,
                ("$w", weekTemplateId), ("$d", slot.DayOfWeek), ("$l", slot.LessonNumber),
                ("$c", classId), ("$s", subjectId), ("$t", teacherId), ("$r", roomId),
                ("$g", slot.SubgroupIndex), ("$p", (object?)slot.WeekParity ?? DBNull.Value),
                ("$bn", (object?)slot.BellTemplateName ?? DBNull.Value),
                ("$ip", slot.IsPartial ? 1 : 0),
                ("$pk", (object?)slot.PartialKind ?? DBNull.Value),
                ("$pm", (object?)slot.PartialMinutes ?? DBNull.Value));
            count++;
        }

        ctx.Stats[AppDataTransferSection.Schedule] = $"{templates.Count} шаблонов, {count} уроков";
    }

    private async Task ImportCalendarAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        if (ctx.Mode == AppDataImportMode.Replace)
        {
            await ExecuteAsync(conn, tx, "DELETE FROM calendar_exceptions");
            await ExecuteAsync(conn, tx, "DELETE FROM schedule_periods");
        }

        var periods = await ReadSourceSchedulePeriodsAsync(conn, tx);
        foreach (var period in periods)
        {
            await ExecuteAsync(conn, tx, """
                INSERT INTO schedule_periods (name, period_type, start_date, end_date, recurrence_cycle)
                VALUES ($n, $t, $s, $e, $r)
                """,
                ("$n", period.Name), ("$t", period.PeriodType),
                ("$s", period.StartDate), ("$e", period.EndDate), ("$r", period.RecurrenceCycle));
        }

        var exceptions = await ReadSourceCalendarExceptionsAsync(conn, tx);
        foreach (var item in exceptions)
        {
            int? weekTemplateId = item.WeekTemplateId is int wt && ctx.WeekTemplateIds.TryGetValue(wt, out var mapped)
                ? mapped
                : null;

            await ExecuteAsync(conn, tx, """
                INSERT INTO calendar_exceptions
                (start_date, end_date, exception_type, donor_day_of_week, week_template_id, note)
                VALUES ($s, $e, $t, $d, $w, $n)
                """,
                ("$s", item.StartDate), ("$e", (object?)item.EndDate ?? DBNull.Value),
                ("$t", item.ExceptionType), ("$d", (object?)item.DonorDayOfWeek ?? DBNull.Value),
                ("$w", (object?)weekTemplateId ?? DBNull.Value), ("$n", (object?)item.Note ?? DBNull.Value));
        }

        ctx.Stats[AppDataTransferSection.Calendar] = $"{periods.Count} периодов, {exceptions.Count} исключений";
    }

    private async Task ImportDayOperationsAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        await EnsureClassMapFromKeysAsync(conn, tx, ctx);
        await EnsureTeacherMapFromNamesAsync(conn, tx, ctx);
        await EnsureRoomMapFromKeysAsync(conn, tx, ctx);
        await EnsureBellTemplateMapFromNamesAsync(conn, tx, ctx);

        if (ctx.Mode == AppDataImportMode.Replace)
        {
            await ExecuteAsync(conn, tx, "DELETE FROM substitution_records");
            await ExecuteAsync(conn, tx, "DELETE FROM day_overrides");
        }

        var overrides = await ReadSourceDayOverridesAsync(conn, tx);
        foreach (var row in overrides)
        {
            int? classId = row.ClassId is int c && ctx.ClassIds.TryGetValue(c, out var cid) ? cid : null;
            int? teacherId = row.TeacherId is int t && ctx.TeacherIds.TryGetValue(t, out var tid) ? tid : null;
            int? replacementId = row.ReplacementTeacherId is int rt && ctx.TeacherIds.TryGetValue(rt, out var rtid) ? rtid : null;
            int? roomId = row.RoomId is int r && ctx.RoomIds.TryGetValue(r, out var rid) ? rid : null;
            int? bellId = row.BellTemplateId is int b && ctx.BellTemplateIds.TryGetValue(b, out var bid) ? bid : null;

            await ExecuteAsync(conn, tx, """
                INSERT INTO day_overrides
                (date, override_type, class_id, lesson_number, teacher_id, replacement_teacher_id, room_id, bell_template_id, note)
                VALUES ($d, $t, $c, $l, $te, $re, $r, $b, $n)
                """,
                ("$d", row.Date), ("$t", row.OverrideType),
                ("$c", (object?)classId ?? DBNull.Value), ("$l", (object?)row.LessonNumber ?? DBNull.Value),
                ("$te", (object?)teacherId ?? DBNull.Value), ("$re", (object?)replacementId ?? DBNull.Value),
                ("$r", (object?)roomId ?? DBNull.Value), ("$b", (object?)bellId ?? DBNull.Value),
                ("$n", (object?)row.Note ?? DBNull.Value));
        }

        var substitutions = await ReadSourceSubstitutionRecordsAsync(conn, tx);
        foreach (var row in substitutions)
        {
            if (!ctx.TeacherIds.TryGetValue(row.ReplacementTeacherId, out var replacementId))
                continue;

            int? absentId = row.AbsentTeacherId is int at && ctx.TeacherIds.TryGetValue(at, out var atid) ? atid : null;
            if (absentId is null)
                continue;

            int? classId = row.ClassId is int c && ctx.ClassIds.TryGetValue(c, out var cid) ? cid : null;
            int? subjectId = row.SubjectId is int s && ctx.SubjectIds.TryGetValue(s, out var sid) ? sid : null;

            await ExecuteAsync(conn, tx, """
                INSERT INTO substitution_records
                (date, lesson_number, class_id, class_name, class_shift, subject_id, subject_name,
                 absent_teacher_id, absent_teacher_name, replacement_teacher_id, replacement_teacher_name,
                 start_time, end_time, is_official, source, note, day_override_id)
                VALUES ($d, $l, $c, $cn, $sh, $s, $sn, $at, $atn, $rt, $rtn, $st, $en, $o, $src, $n, $ov)
                """,
                ("$d", row.Date), ("$l", row.LessonNumber),
                ("$c", (object?)classId ?? DBNull.Value), ("$cn", row.ClassName), ("$sh", row.ClassShift),
                ("$s", (object?)subjectId ?? DBNull.Value), ("$sn", row.SubjectName),
                ("$at", absentId.Value), ("$atn", row.AbsentTeacherName),
                ("$rt", replacementId), ("$rtn", row.ReplacementTeacherName),
                ("$st", row.StartTime), ("$en", row.EndTime),
                ("$o", row.IsOfficial ? 1 : 0), ("$src", row.Source),
                ("$n", (object?)row.Note ?? DBNull.Value),
                ("$ov", (object?)row.DayOverrideId ?? DBNull.Value));
        }

        ctx.Stats[AppDataTransferSection.DayOperations] = $"{overrides.Count} правок, {substitutions.Count} замен";
    }

    // --- helpers: maps from names when dependency section not imported ---

    private static async Task EnsureBuildingMapFromNamesAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        if (ctx.BuildingIds.Count > 0)
            return;

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"""
            SELECT s.id, s.name
            FROM {SourceAlias}.buildings s
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var srcId = reader.GetInt32(0);
            var name = reader.GetString(1);
            var targetId = await FindBuildingIdByNameAsync(conn, tx, name);
            if (targetId is int id)
                ctx.BuildingIds[srcId] = id;
        }
    }

    private static async Task EnsureSubjectMapFromNamesAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        if (ctx.SubjectIds.Count > 0)
            return;

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT id, name FROM {SourceAlias}.subjects";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var srcId = reader.GetInt32(0);
            var name = reader.GetString(1);
            var targetId = await FindSubjectIdByNameAsync(conn, tx, name);
            if (targetId is int id)
                ctx.SubjectIds[srcId] = id;
        }
    }

    private static async Task EnsureTeacherMapFromNamesAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        if (ctx.TeacherIds.Count > 0)
            return;

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT id, full_name FROM {SourceAlias}.teachers";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var srcId = reader.GetInt32(0);
            var name = reader.GetString(1);
            var targetId = await FindTeacherIdByNameAsync(conn, tx, name);
            if (targetId is int id)
                ctx.TeacherIds[srcId] = id;
        }
    }

    private static async Task EnsureClassMapFromKeysAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        if (ctx.ClassIds.Count > 0)
            return;

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT id, grade, letter, shift FROM {SourceAlias}.school_classes";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var srcId = reader.GetInt32(0);
            var targetId = await FindClassIdAsync(conn, tx, reader.GetInt32(1), reader.GetString(2), reader.GetInt32(3));
            if (targetId is int id)
                ctx.ClassIds[srcId] = id;
        }
    }

    private static async Task EnsureRoomMapFromKeysAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        if (ctx.RoomIds.Count > 0)
            return;

        await EnsureBuildingMapFromNamesAsync(conn, tx, ctx);
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"""
            SELECT r.id, r.number, r.building_id
            FROM {SourceAlias}.rooms r
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var srcId = reader.GetInt32(0);
            var number = reader.GetString(1);
            var buildingId = reader.GetInt32(2);
            if (!ctx.BuildingIds.TryGetValue(buildingId, out var targetBuildingId))
                continue;
            var targetId = await FindRoomIdAsync(conn, tx, number, targetBuildingId);
            if (targetId is int id)
                ctx.RoomIds[srcId] = id;
        }
    }

    private static async Task EnsureBellTemplateMapFromNamesAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        if (ctx.BellTemplateIds.Count > 0)
            return;

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT id, name FROM {SourceAlias}.bell_templates";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var srcId = reader.GetInt32(0);
            var targetId = await FindBellTemplateIdByNameAsync(conn, tx, reader.GetString(1));
            if (targetId is int id)
                ctx.BellTemplateIds[srcId] = id;
        }
    }

    private static async Task ImportTeacherChildTablesAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        await EnsureSubjectMapFromNamesAsync(conn, tx, ctx);
        await EnsureClassMapFromKeysAsync(conn, tx, ctx);

        foreach (var (srcTeacherId, targetTeacherId) in ctx.TeacherIds)
        {
            if (ctx.Mode == AppDataImportMode.Replace || ctx.Mode == AppDataImportMode.Merge)
            {
                await ExecuteAsync(conn, tx, "DELETE FROM teacher_subjects WHERE teacher_id = $id", ("$id", targetTeacherId));
                if (await TargetHasTableAsync(conn, tx, "teacher_class_subjects"))
                    await ExecuteAsync(conn, tx, "DELETE FROM teacher_class_subjects WHERE teacher_id = $id", ("$id", targetTeacherId));
                await ExecuteAsync(conn, tx, "DELETE FROM teacher_curriculum_items WHERE teacher_id = $id", ("$id", targetTeacherId));
                await ExecuteAsync(conn, tx, "DELETE FROM teacher_preferred_classes WHERE teacher_id = $id", ("$id", targetTeacherId));
                await ExecuteAsync(conn, tx, "DELETE FROM teacher_building_days WHERE teacher_id = $id", ("$id", targetTeacherId));
                await ExecuteAsync(conn, tx, "DELETE FROM teacher_status_periods WHERE teacher_id = $id", ("$id", targetTeacherId));
                await ExecuteAsync(conn, tx, "DELETE FROM teacher_unavailability WHERE teacher_id = $id", ("$id", targetTeacherId));
            }
        }

        await CopyTeacherSubjectsAsync(conn, tx, ctx);
        await CopyTeacherClassSubjectsAsync(conn, tx, ctx);
        await CopyTeacherCurriculumItemsAsync(conn, tx, ctx);
        await CopyTeacherPreferredClassesAsync(conn, tx, ctx);
        await CopyTeacherBuildingDaysAsync(conn, tx, ctx);
        await CopyTeacherStatusPeriodsAsync(conn, tx, ctx);
        await CopyTeacherUnavailabilityAsync(conn, tx, ctx);
        await CopyClassTeachersAsync(conn, tx, ctx);
    }

    private static async Task ImportClassTeachersAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT teacher_id, class_id FROM {SourceAlias}.class_teachers";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var srcTeacher = reader.GetInt32(0);
            var srcClass = reader.GetInt32(1);
            if (!ctx.TeacherIds.TryGetValue(srcTeacher, out var teacherId)
                || !ctx.ClassIds.TryGetValue(srcClass, out var classId))
                continue;

            await ExecuteAsync(conn, tx, """
                INSERT INTO class_teachers (teacher_id, class_id) VALUES ($t, $c)
                ON CONFLICT(teacher_id, class_id) DO NOTHING
                """,
                ("$t", teacherId), ("$c", classId));
        }
    }

    private static async Task CopyTeacherSubjectsAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT teacher_id, subject_id, profile_type FROM {SourceAlias}.teacher_subjects";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!ctx.TeacherIds.TryGetValue(reader.GetInt32(0), out var teacherId)
                || !ctx.SubjectIds.TryGetValue(reader.GetInt32(1), out var subjectId))
                continue;

            await ExecuteAsync(conn, tx,
                "INSERT INTO teacher_subjects (teacher_id, subject_id, profile_type) VALUES ($t, $s, $p)",
                ("$t", teacherId), ("$s", subjectId), ("$p", reader.GetString(2)));
        }
    }

    private static async Task CopyTeacherClassSubjectsAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        if (!await SourceHasTableAsync(conn, tx, "teacher_class_subjects"))
            return;

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT teacher_id, class_id, subject_id FROM {SourceAlias}.teacher_class_subjects";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!ctx.TeacherIds.TryGetValue(reader.GetInt32(0), out var teacherId)
                || !ctx.ClassIds.TryGetValue(reader.GetInt32(1), out var classId)
                || !ctx.SubjectIds.TryGetValue(reader.GetInt32(2), out var subjectId))
                continue;

            await ExecuteAsync(conn, tx, """
                INSERT OR IGNORE INTO teacher_class_subjects (teacher_id, class_id, subject_id)
                VALUES ($t, $c, $s)
                """,
                ("$t", teacherId), ("$c", classId), ("$s", subjectId));
        }
    }

    private static async Task CopyTeacherCurriculumItemsAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"""
            SELECT tci.teacher_id, cu.class_id, cu.subject_id, cu.week_parity
            FROM {SourceAlias}.teacher_curriculum_items tci
            JOIN {SourceAlias}.curriculum cu ON cu.id = tci.curriculum_id
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!ctx.TeacherIds.TryGetValue(reader.GetInt32(0), out var teacherId)
                || !ctx.ClassIds.TryGetValue(reader.GetInt32(1), out var classId)
                || !ctx.SubjectIds.TryGetValue(reader.GetInt32(2), out var subjectId))
                continue;

            var weekParity = reader.GetString(3);
            var curriculumId = await FindCurriculumIdAsync(conn, tx, classId, subjectId, weekParity);
            if (curriculumId is null)
                continue;

            await ExecuteAsync(conn, tx, """
                INSERT INTO teacher_curriculum_items (teacher_id, curriculum_id, source)
                VALUES ($t, $c, 'explicit')
                ON CONFLICT(teacher_id, curriculum_id) DO NOTHING
                """,
                ("$t", teacherId), ("$c", curriculumId.Value));
        }
    }

    private static async Task<int?> FindCurriculumIdAsync(
        SqliteConnection conn, SqliteTransaction tx, int classId, int subjectId, string weekParity)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT id FROM curriculum
            WHERE class_id = $c AND subject_id = $s AND week_parity = $p
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$c", classId);
        cmd.Parameters.AddWithValue("$s", subjectId);
        cmd.Parameters.AddWithValue("$p", weekParity);
        var result = await cmd.ExecuteScalarAsync();
        return result is long l ? (int)l : null;
    }

    private static async Task CopyTeacherPreferredClassesAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT teacher_id, class_id FROM {SourceAlias}.teacher_preferred_classes";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!ctx.TeacherIds.TryGetValue(reader.GetInt32(0), out var teacherId)
                || !ctx.ClassIds.TryGetValue(reader.GetInt32(1), out var classId))
                continue;

            await ExecuteAsync(conn, tx,
                "INSERT INTO teacher_preferred_classes (teacher_id, class_id) VALUES ($t, $c)",
                ("$t", teacherId), ("$c", classId));
        }
    }

    private static async Task CopyTeacherBuildingDaysAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT teacher_id, building_id, day_of_week FROM {SourceAlias}.teacher_building_days";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!ctx.TeacherIds.TryGetValue(reader.GetInt32(0), out var teacherId)
                || !ctx.BuildingIds.TryGetValue(reader.GetInt32(1), out var buildingId))
                continue;

            await ExecuteAsync(conn, tx,
                "INSERT INTO teacher_building_days (teacher_id, building_id, day_of_week) VALUES ($t, $b, $d)",
                ("$t", teacherId), ("$b", buildingId), ("$d", reader.GetInt32(2)));
        }
    }

    private static async Task CopyTeacherStatusPeriodsAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        if (!await SourceHasTableAsync(conn, tx, "teacher_status_periods"))
            return;

        var hasOfficial = await SourceHasColumnAsync(conn, tx, "teacher_status_periods", "is_official");
        var statusColumn = await SourceHasColumnAsync(conn, tx, "teacher_status_periods", "status_type")
            ? "status_type"
            : "status_kind";

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = hasOfficial
            ? $"""
                SELECT teacher_id, {statusColumn}, start_date, end_date, note, COALESCE(is_official, 0), COALESCE(source, 'profile')
                FROM {SourceAlias}.teacher_status_periods
                """
            : $"""
                SELECT teacher_id, {statusColumn}, start_date, end_date, note
                FROM {SourceAlias}.teacher_status_periods
                """;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!ctx.TeacherIds.TryGetValue(reader.GetInt32(0), out var teacherId))
                continue;

            var isOfficial = hasOfficial && reader.GetInt32(5) != 0;
            var source = hasOfficial && !reader.IsDBNull(6) ? reader.GetString(6) : "profile";

            await ExecuteAsync(conn, tx, """
                INSERT INTO teacher_status_periods
                (teacher_id, status_type, start_date, end_date, note, is_official, source)
                VALUES ($t, $s, $from, $to, $n, $o, $src)
                """,
                ("$t", teacherId),
                ("$s", reader.GetString(1)),
                ("$from", reader.GetString(2)),
                ("$to", reader.IsDBNull(3) ? DBNull.Value : reader.GetString(3)),
                ("$n", reader.IsDBNull(4) ? DBNull.Value : reader.GetString(4)),
                ("$o", isOfficial ? 1 : 0),
                ("$src", source));
        }
    }

    private static async Task CopyTeacherUnavailabilityAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx)
    {
        if (!await SourceHasTableAsync(conn, tx, "teacher_unavailability"))
            return;

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"""
            SELECT teacher_id, recurrence_type, day_of_week, start_date, end_date,
                   COALESCE(all_day, 1), lesson_from, lesson_to, note
            FROM {SourceAlias}.teacher_unavailability
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!ctx.TeacherIds.TryGetValue(reader.GetInt32(0), out var teacherId))
                continue;

            await ExecuteAsync(conn, tx, """
                INSERT INTO teacher_unavailability
                (teacher_id, recurrence_type, day_of_week, start_date, end_date, all_day, lesson_from, lesson_to, note)
                VALUES ($t, $r, $dow, $from, $to, $all, $lf, $lt, $n)
                """,
                ("$t", teacherId),
                ("$r", reader.GetString(1)),
                ("$dow", reader.IsDBNull(2) ? DBNull.Value : reader.GetInt32(2)),
                ("$from", reader.GetString(3)),
                ("$to", reader.IsDBNull(4) ? DBNull.Value : reader.GetString(4)),
                ("$all", reader.GetInt32(5)),
                ("$lf", reader.IsDBNull(6) ? DBNull.Value : reader.GetInt32(6)),
                ("$lt", reader.IsDBNull(7) ? DBNull.Value : reader.GetInt32(7)),
                ("$n", reader.IsDBNull(8) ? DBNull.Value : reader.GetString(8)));
        }
    }

    private static async Task CopyClassTeachersAsync(SqliteConnection conn, SqliteTransaction tx, ImportContext ctx) =>
        await ImportClassTeachersAsync(conn, tx, ctx);

    private static async Task ClearTeacherTablesAsync(SqliteConnection conn, SqliteTransaction tx, bool clearSlots)
    {
        if (clearSlots)
            await ExecuteAsync(conn, tx, "DELETE FROM week_template_slots");

        await ExecuteAsync(conn, tx, "DELETE FROM day_overrides");
        await ExecuteAsync(conn, tx, "DELETE FROM teacher_status_periods");
        await ExecuteAsync(conn, tx, "DELETE FROM teacher_unavailability");
        await ExecuteAsync(conn, tx, "DELETE FROM teacher_subjects");
        if (await TargetHasTableAsync(conn, tx, "teacher_class_subjects"))
            await ExecuteAsync(conn, tx, "DELETE FROM teacher_class_subjects");
        await ExecuteAsync(conn, tx, "DELETE FROM teacher_curriculum_items");
        await ExecuteAsync(conn, tx, "DELETE FROM teacher_preferred_classes");
        await ExecuteAsync(conn, tx, "DELETE FROM teacher_building_days");
        await ExecuteAsync(conn, tx, "DELETE FROM class_teachers");
        await ExecuteAsync(conn, tx, "UPDATE rooms SET assigned_teacher_id = NULL WHERE assigned_teacher_id IS NOT NULL");
        await ExecuteAsync(conn, tx, "UPDATE teachers SET room_id = NULL WHERE room_id IS NOT NULL");
        await ExecuteAsync(conn, tx, "DELETE FROM teachers");
    }

    private static async Task<Dictionary<int, string>> SnapshotSlotTeacherNamesAsync(SqliteConnection conn, SqliteTransaction tx)
    {
        var result = new Dictionary<int, string>();
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT wts.id, t.full_name
            FROM week_template_slots wts
            JOIN teachers t ON t.id = wts.teacher_id
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result[reader.GetInt32(0)] = reader.GetString(1);
        return result;
    }

    private static async Task RemapSlotTeachersByNameAsync(
        SqliteConnection conn,
        SqliteTransaction tx,
        Dictionary<int, string> slotTeacherNames)
    {
        foreach (var (slotId, teacherName) in slotTeacherNames)
        {
            var teacherId = await FindTeacherIdByNameAsync(conn, tx, teacherName);
            if (teacherId is int id)
            {
                await ExecuteAsync(conn, tx,
                    "UPDATE week_template_slots SET teacher_id = $t WHERE id = $id",
                    ("$t", id), ("$id", slotId));
            }
        }
    }

    private static async Task AttachSourceAsync(SqliteConnection conn, string sourceDbPath)
    {
        if (!File.Exists(sourceDbPath))
            throw new FileNotFoundException("Файл базы для импорта не найден.", sourceDbPath);

        var escaped = EscapeAttachPath(sourceDbPath);
        await ExecuteAsync(conn, null, $"ATTACH DATABASE '{escaped}' AS {SourceAlias}");
    }

    private static async Task DetachSourceAsync(SqliteConnection conn)
    {
        try
        {
            await ExecuteAsync(conn, null, $"DETACH DATABASE {SourceAlias}");
        }
        catch (SqliteException)
        {
            // already detached
        }
    }

    private static string EscapeAttachPath(string sourceDbPath) =>
        Path.GetFullPath(sourceDbPath).Replace('\\', '/').Replace("'", "''");

    private static async Task<bool> TableHasRowsAsync(SqliteConnection conn, SqliteTransaction tx, string table)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT 1 FROM {table} LIMIT 1";
        return await cmd.ExecuteScalarAsync() is not null;
    }

    private static async Task<int?> FindBuildingIdByNameAsync(SqliteConnection conn, SqliteTransaction tx, string name)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id FROM buildings WHERE lower(trim(name)) = lower(trim($n)) LIMIT 1";
        cmd.Parameters.AddWithValue("$n", name);
        var result = await cmd.ExecuteScalarAsync();
        return result is long l ? (int)l : null;
    }

    private static async Task<int?> FindSubjectIdByNameAsync(SqliteConnection conn, SqliteTransaction tx, string name)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id FROM subjects WHERE lower(trim(name)) = lower(trim($n)) LIMIT 1";
        cmd.Parameters.AddWithValue("$n", name);
        var result = await cmd.ExecuteScalarAsync();
        return result is long l ? (int)l : null;
    }

    private static async Task<int?> FindTeacherIdByNameAsync(SqliteConnection conn, SqliteTransaction tx, string name)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id FROM teachers WHERE lower(trim(full_name)) = lower(trim($n)) LIMIT 1";
        cmd.Parameters.AddWithValue("$n", name);
        var result = await cmd.ExecuteScalarAsync();
        return result is long l ? (int)l : null;
    }

    private static async Task<int?> FindClassIdAsync(SqliteConnection conn, SqliteTransaction tx, int grade, string letter, int shift)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT id FROM school_classes
            WHERE grade = $g AND lower(trim(letter)) = lower(trim($l)) AND shift = $s
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$g", grade);
        cmd.Parameters.AddWithValue("$l", letter);
        cmd.Parameters.AddWithValue("$s", shift);
        var result = await cmd.ExecuteScalarAsync();
        return result is long l ? (int)l : null;
    }

    private static async Task<int?> FindRoomIdAsync(SqliteConnection conn, SqliteTransaction tx, string number, int buildingId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT id FROM rooms
            WHERE lower(trim(number)) = lower(trim($n)) AND building_id = $b
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$n", number);
        cmd.Parameters.AddWithValue("$b", buildingId);
        var result = await cmd.ExecuteScalarAsync();
        return result is long l ? (int)l : null;
    }

    private static async Task<int?> FindBellTemplateIdByNameAsync(SqliteConnection conn, SqliteTransaction tx, string name)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id FROM bell_templates WHERE lower(trim(name)) = lower(trim($n)) LIMIT 1";
        cmd.Parameters.AddWithValue("$n", name);
        var result = await cmd.ExecuteScalarAsync();
        return result is long l ? (int)l : null;
    }

    private static async Task<int?> FindWeekTemplateIdByNameAsync(SqliteConnection conn, SqliteTransaction tx, string name)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id FROM week_templates WHERE lower(trim(name)) = lower(trim($n)) LIMIT 1";
        cmd.Parameters.AddWithValue("$n", name);
        var result = await cmd.ExecuteScalarAsync();
        return result is long l ? (int)l : null;
    }

    private static async Task<int?> FindCurriculumTemplateIdAsync(SqliteConnection conn, SqliteTransaction tx, string name)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id FROM curriculum_templates WHERE lower(trim(name)) = lower(trim($n)) LIMIT 1";
        cmd.Parameters.AddWithValue("$n", name);
        var result = await cmd.ExecuteScalarAsync();
        return result is long l ? (int)l : null;
    }

    private static async Task<int> InsertScalarAsync(
        SqliteConnection conn,
        SqliteTransaction tx,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task ExecuteAsync(
        SqliteConnection conn,
        SqliteTransaction? tx,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var cmd = conn.CreateCommand();
        if (tx is not null)
            cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task UpdateTeacherAsync(SqliteConnection conn, SqliteTransaction tx, int id, TeacherRow row)
    {
        await ExecuteAsync(conn, tx, """
            UPDATE teachers SET teacher_type = $t, max_load_hours = $m, room_id = NULL,
                job_title = $j, phone = $p, contact_url = $u, contact_note = $c, works_with_first_grade = $g
            WHERE id = $id
            """,
            ("$t", row.TeacherType), ("$m", row.MaxLoadHours),
            ("$j", (object?)row.JobTitle ?? DBNull.Value),
            ("$p", (object?)row.Phone ?? DBNull.Value),
            ("$u", (object?)row.ContactUrl ?? DBNull.Value),
            ("$c", (object?)row.ContactNote ?? DBNull.Value),
            ("$g", row.WorksWithFirstGrade ? 1 : 0),
            ("$id", id));
    }

    private static async Task<int> InsertTeacherAsync(SqliteConnection conn, SqliteTransaction tx, TeacherRow row) =>
        await InsertScalarAsync(conn, tx, """
            INSERT INTO teachers
            (full_name, teacher_type, max_load_hours, room_id, job_title, phone, contact_url, contact_note, works_with_first_grade)
            VALUES ($n, $t, $m, NULL, $j, $p, $u, $c, $g); SELECT last_insert_rowid();
            """,
            ("$n", row.FullName), ("$t", row.TeacherType), ("$m", row.MaxLoadHours),
            ("$j", (object?)row.JobTitle ?? DBNull.Value),
            ("$p", (object?)row.Phone ?? DBNull.Value),
            ("$u", (object?)row.ContactUrl ?? DBNull.Value),
            ("$c", (object?)row.ContactNote ?? DBNull.Value),
            ("$g", row.WorksWithFirstGrade ? 1 : 0));

    // --- source readers (abbreviated structs inline) ---

    private sealed class ImportContext(AppDataImportMode mode)
    {
        public AppDataImportMode Mode { get; } = mode;
        public Dictionary<int, int> BuildingIds { get; } = [];
        public Dictionary<int, int> SubjectIds { get; } = [];
        public Dictionary<int, int> TeacherIds { get; } = [];
        public Dictionary<int, int> ClassIds { get; } = [];
        public Dictionary<int, int> RoomIds { get; } = [];
        public Dictionary<int, int> BellTemplateIds { get; } = [];
        public Dictionary<int, int> WeekTemplateIds { get; } = [];
        public Dictionary<int, int> CurriculumTemplateIds { get; } = [];
        public Dictionary<AppDataTransferSection, string> Stats { get; } = [];
        public List<string> Warnings { get; } = [];
    }

    private sealed record BuildingRow(int SourceId, string Name, string ColorHex);
    private sealed record RouteRow(int FromId, int ToId, int Minutes);
    private sealed record SubjectRow(int SourceId, string Name, double DifficultyScore);
    private sealed record TeacherRow(
        int SourceId, string FullName, string TeacherType, int MaxLoadHours,
        string? JobTitle, string? Phone, string? ContactUrl, string? ContactNote, bool WorksWithFirstGrade);
    private sealed record RoomRow(int SourceId, string Number, int BuildingId, int Capacity, string RoomKind, int? AssignedTeacherId);
    private sealed record ClassRow(
        int SourceId, int Grade, string Letter, int Shift, int StudentCount, bool IsCorrectional,
        int? BuildingId, int? DefaultRoomId, int? DefaultPeRoomId, int? BellTemplateId);
    private sealed record CurriculumRow(int ClassId, int SubjectId, double HoursPerWeek, bool HasSubgroups, string WeekParity);
    private sealed record CurriculumTemplateRow(int SourceId, string Name, int GradeFrom, int GradeTo, bool IsBuiltIn, int SortOrder);
    private sealed record CurriculumTemplateItemRow(
        int TemplateId, string SubjectName, double HoursPerWeek, double DifficultyScore,
        bool HasSubgroups, string WeekParity, int ItemGradeFrom, int ItemGradeTo);
    private sealed record BellTemplateRow(int SourceId, string Name);
    private sealed record BellPeriodRow(int TemplateId, int LessonNumber, int Shift, string StartTime, string EndTime, string PeriodKind);
    private sealed record WeekTemplateRow(int SourceId, string Name);
    private sealed record SlotRow(
        int WeekTemplateId, int DayOfWeek, int LessonNumber, int ClassId, int SubjectId, int TeacherId, int RoomId,
        int SubgroupIndex, string? WeekParity, string? BellTemplateName, bool IsPartial, string? PartialKind, int? PartialMinutes);
    private sealed record SchedulePeriodRow(string Name, string PeriodType, string StartDate, string EndDate, string RecurrenceCycle);
    private sealed record CalendarExceptionRow(
        string StartDate, string? EndDate, string ExceptionType, int? DonorDayOfWeek, int? WeekTemplateId, string? Note);
    private sealed record DayOverrideRow(
        string Date, string OverrideType, int? ClassId, int? LessonNumber, int? TeacherId,
        int? ReplacementTeacherId, int? RoomId, int? BellTemplateId, string? Note);
    private sealed record SubstitutionRecordRow(
        string Date, int LessonNumber, int ClassShift, string ClassName, string SubjectName,
        int? AbsentTeacherId, string AbsentTeacherName, int ReplacementTeacherId, string ReplacementTeacherName,
        int? ClassId, int? SubjectId, string StartTime, string EndTime, bool IsOfficial, string Source,
        int? DayOverrideId, string? Note);

    private static async Task<List<BuildingRow>> ReadSourceBuildingsAsync(SqliteConnection conn, SqliteTransaction tx)
    {
        var list = new List<BuildingRow>();
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT id, name, color_hex FROM {SourceAlias}.buildings ORDER BY id";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(new BuildingRow(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        return list;
    }

    private static async Task<List<RouteRow>> ReadSourceRoutesAsync(SqliteConnection conn, SqliteTransaction tx)
    {
        var list = new List<RouteRow>();
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT from_building_id, to_building_id, minutes FROM {SourceAlias}.building_routes";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(new RouteRow(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2)));
        return list;
    }

    private static async Task<List<SubjectRow>> ReadSourceSubjectsAsync(SqliteConnection conn, SqliteTransaction tx)
    {
        var list = new List<SubjectRow>();
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT id, name, difficulty_score FROM {SourceAlias}.subjects ORDER BY id";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(new SubjectRow(reader.GetInt32(0), reader.GetString(1), reader.GetDouble(2)));
        return list;
    }

    private static async Task<List<TeacherRow>> ReadSourceTeachersAsync(SqliteConnection conn, SqliteTransaction tx)
    {
        var list = new List<TeacherRow>();
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"""
            SELECT id, full_name, teacher_type, max_load_hours, job_title, phone, contact_url, contact_note,
                   COALESCE(works_with_first_grade, 0)
            FROM {SourceAlias}.teachers ORDER BY id
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new TeacherRow(
                reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetInt32(8) != 0));
        }

        return list;
    }

    private static async Task<List<RoomRow>> ReadSourceRoomsAsync(SqliteConnection conn, SqliteTransaction tx)
    {
        var list = new List<RoomRow>();
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT id, number, building_id, capacity, room_kind, assigned_teacher_id FROM {SourceAlias}.rooms";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new RoomRow(
                reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5)));
        }

        return list;
    }

    private static async Task<List<ClassRow>> ReadSourceClassesAsync(SqliteConnection conn, SqliteTransaction tx)
    {
        var list = new List<ClassRow>();
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"""
            SELECT id, grade, letter, shift, student_count, COALESCE(is_correctional, 0),
                   building_id, default_room_id, default_pe_room_id, bell_template_id
            FROM {SourceAlias}.school_classes
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new ClassRow(
                reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetInt32(3), reader.GetInt32(4),
                reader.GetInt32(5) != 0,
                reader.IsDBNull(6) ? null : reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetInt32(7),
                reader.IsDBNull(8) ? null : reader.GetInt32(8),
                reader.IsDBNull(9) ? null : reader.GetInt32(9)));
        }

        return list;
    }

    private static async Task<List<CurriculumRow>> ReadSourceCurriculumAsync(SqliteConnection conn, SqliteTransaction tx)
    {
        var list = new List<CurriculumRow>();
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"""
            SELECT class_id, subject_id, hours_per_week, COALESCE(has_subgroups, 0), COALESCE(week_parity, '')
            FROM {SourceAlias}.curriculum
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new CurriculumRow(
                reader.GetInt32(0), reader.GetInt32(1), reader.GetDouble(2),
                reader.GetInt32(3) != 0, reader.GetString(4)));
        }

        return list;
    }

    private static async Task<List<CurriculumTemplateRow>> ReadSourceCurriculumTemplatesAsync(SqliteConnection conn, SqliteTransaction tx)
    {
        var list = new List<CurriculumTemplateRow>();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = $"SELECT id, name, grade_from, grade_to, COALESCE(is_builtin, 0), COALESCE(sort_order, 0) FROM {SourceAlias}.curriculum_templates";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add(new CurriculumTemplateRow(
                    reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3),
                    reader.GetInt32(4) != 0, reader.GetInt32(5)));
        }
        catch (SqliteException)
        {
            // table may be absent in very old archives
        }

        return list;
    }

    private static async Task<List<CurriculumTemplateItemRow>> ReadSourceCurriculumTemplateItemsAsync(SqliteConnection conn, SqliteTransaction tx)
    {
        var list = new List<CurriculumTemplateItemRow>();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = $"""
                SELECT template_id, subject_name, hours_per_week, difficulty_score, COALESCE(has_subgroups, 0),
                       COALESCE(week_parity, 'EveryWeek'), COALESCE(item_grade_from, 0), COALESCE(item_grade_to, 0)
                FROM {SourceAlias}.curriculum_template_items
                """;
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new CurriculumTemplateItemRow(
                    reader.GetInt32(0), reader.GetString(1), reader.GetDouble(2), reader.GetDouble(3),
                    reader.GetInt32(4) != 0, reader.GetString(5), reader.GetInt32(6), reader.GetInt32(7)));
            }
        }
        catch (SqliteException)
        {
        }

        return list;
    }

    private static async Task<List<BellTemplateRow>> ReadSourceBellTemplatesAsync(SqliteConnection conn, SqliteTransaction tx)
    {
        var list = new List<BellTemplateRow>();
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT id, name FROM {SourceAlias}.bell_templates";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(new BellTemplateRow(reader.GetInt32(0), reader.GetString(1)));
        return list;
    }

    private static async Task<List<BellPeriodRow>> ReadSourceBellPeriodsAsync(SqliteConnection conn, SqliteTransaction tx)
    {
        var list = new List<BellPeriodRow>();
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"""
            SELECT template_id, lesson_number, shift, start_time, end_time, period_kind
            FROM {SourceAlias}.bell_periods
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new BellPeriodRow(
                reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2),
                reader.GetString(3), reader.GetString(4), reader.GetString(5)));
        }

        return list;
    }

    private static async Task<List<WeekTemplateRow>> ReadSourceWeekTemplatesAsync(SqliteConnection conn, SqliteTransaction tx)
    {
        var list = new List<WeekTemplateRow>();
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT id, name FROM {SourceAlias}.week_templates";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(new WeekTemplateRow(reader.GetInt32(0), reader.GetString(1)));
        return list;
    }

    private static async Task<List<SlotRow>> ReadSourceSlotsAsync(SqliteConnection conn, SqliteTransaction tx)
    {
        var list = new List<SlotRow>();
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"""
            SELECT week_template_id, day_of_week, lesson_number, class_id, subject_id, teacher_id, room_id,
                   COALESCE(subgroup_index, 0), week_parity, bell_template_name,
                   COALESCE(is_partial, 0), partial_kind, partial_minutes
            FROM {SourceAlias}.week_template_slots
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new SlotRow(
                reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3),
                reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6), reader.GetInt32(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.GetInt32(10) != 0,
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.IsDBNull(12) ? null : reader.GetInt32(12)));
        }

        return list;
    }

    private static async Task<List<SchedulePeriodRow>> ReadSourceSchedulePeriodsAsync(SqliteConnection conn, SqliteTransaction tx)
    {
        var list = new List<SchedulePeriodRow>();
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT name, period_type, start_date, end_date, recurrence_cycle FROM {SourceAlias}.schedule_periods";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new SchedulePeriodRow(
                reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)));
        }

        return list;
    }

    private static async Task<List<CalendarExceptionRow>> ReadSourceCalendarExceptionsAsync(SqliteConnection conn, SqliteTransaction tx)
    {
        var list = new List<CalendarExceptionRow>();
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"""
            SELECT start_date, end_date, exception_type, donor_day_of_week, week_template_id, note
            FROM {SourceAlias}.calendar_exceptions
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new CalendarExceptionRow(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return list;
    }

    private static async Task<List<DayOverrideRow>> ReadSourceDayOverridesAsync(SqliteConnection conn, SqliteTransaction tx)
    {
        var list = new List<DayOverrideRow>();
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"""
            SELECT date, override_type, class_id, lesson_number, teacher_id, replacement_teacher_id, room_id, bell_template_id, note
            FROM {SourceAlias}.day_overrides
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new DayOverrideRow(
                reader.GetString(0), reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetInt32(7),
                reader.IsDBNull(8) ? null : reader.GetString(8)));
        }

        return list;
    }

    private static async Task<List<SubstitutionRecordRow>> ReadSourceSubstitutionRecordsAsync(SqliteConnection conn, SqliteTransaction tx)
    {
        var list = new List<SubstitutionRecordRow>();
        if (!await SourceHasTableAsync(conn, tx, "substitution_records"))
            return list;

        var hasAbsent = await SourceHasColumnAsync(conn, tx, "substitution_records", "absent_teacher_id");
        var hasShift = await SourceHasColumnAsync(conn, tx, "substitution_records", "class_shift");

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = hasAbsent
            ? $"""
                SELECT date, lesson_number, {(hasShift ? "COALESCE(class_shift, 1)" : "1")}, class_name, subject_name,
                       absent_teacher_id, absent_teacher_name, replacement_teacher_id, replacement_teacher_name,
                       class_id, subject_id, COALESCE(start_time, ''), COALESCE(end_time, ''),
                       COALESCE(is_official, 1), COALESCE(source, 'dispatcher'), day_override_id, note
                FROM {SourceAlias}.substitution_records
                """
            : $"""
                SELECT date, lesson_number, {(hasShift ? "COALESCE(class_shift, 1)" : "1")}, class_name, subject_name,
                       original_teacher_id, '', replacement_teacher_id, '', class_id, NULL,
                       '', '', COALESCE(is_official, 1), 'dispatcher', day_override_id, note
                FROM {SourceAlias}.substitution_records
                """;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new SubstitutionRecordRow(
                reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetString(3), reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5), reader.IsDBNull(6) ? "" : reader.GetString(6),
                reader.GetInt32(7), reader.IsDBNull(8) ? "" : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetInt32(9),
                reader.IsDBNull(10) ? null : reader.GetInt32(10),
                reader.IsDBNull(11) ? "" : reader.GetString(11),
                reader.IsDBNull(12) ? "" : reader.GetString(12),
                reader.GetInt32(13) != 0, reader.IsDBNull(14) ? "dispatcher" : reader.GetString(14),
                reader.IsDBNull(15) ? null : reader.GetInt32(15),
                reader.IsDBNull(16) ? null : reader.GetString(16)));
        }

        return list;
    }

    private static async Task<bool> SourceHasTableAsync(SqliteConnection conn, SqliteTransaction tx, string table)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"""
            SELECT 1 FROM {SourceAlias}.sqlite_master
            WHERE type = 'table' AND name = $n LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$n", table);
        return await cmd.ExecuteScalarAsync() is not null;
    }

    private static async Task<bool> TargetHasTableAsync(SqliteConnection conn, SqliteTransaction tx, string table)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT 1 FROM sqlite_master
            WHERE type = 'table' AND name = $n LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$n", table);
        return await cmd.ExecuteScalarAsync() is not null;
    }

    private static async Task<bool> SourceHasColumnAsync(SqliteConnection conn, SqliteTransaction tx, string table, string column)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"PRAGMA {SourceAlias}.table_info({table})";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader.GetString(1).Equals(column, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
