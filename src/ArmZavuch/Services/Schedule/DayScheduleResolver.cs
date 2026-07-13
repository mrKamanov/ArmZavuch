using ArmZavuch.Data;
using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;
using ArmZavuch.Services.Staff;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Services.Schedule;

/// <summary>
/// Развёртка расписания на дату: календарь → период → шаблон недели → day_overrides (ТЗ §3).
/// </summary>
public sealed class DayScheduleResolver
{
    private readonly SqliteConnectionFactory _factory;
    private readonly WeekTemplateRepository _weekTemplates;
    private readonly SchedulePeriodRepository _periods;
    private readonly DayOverrideRepository _overrides;
    private readonly BellRepository _bells;
    private readonly TeacherAvailabilityService _availability;
    private readonly RoomRepository _rooms;
    private readonly SchoolClassRepository _classes;
    private readonly BellTemplateAssignmentService _bellAssignment;

    public DayScheduleResolver(
        SqliteConnectionFactory factory,
        WeekTemplateRepository weekTemplates,
        SchedulePeriodRepository periods,
        DayOverrideRepository overrides,
        BellRepository bells,
        TeacherAvailabilityService availability,
        RoomRepository rooms,
        SchoolClassRepository classes,
        BellTemplateAssignmentService bellAssignment)
    {
        _factory = factory;
        _weekTemplates = weekTemplates;
        _periods = periods;
        _overrides = overrides;
        _bells = bells;
        _availability = availability;
        _rooms = rooms;
        _classes = classes;
        _bellAssignment = bellAssignment;
    }

    public async Task<(bool IsSchoolDay, List<LessonSlot> Lessons)> ResolveAsync(DateOnly date)
    {
        var calendar = await GetCalendarTypeAsync(date);
        if (calendar == "Holiday" || calendar == "Vacation")
            return (false, []);

        var dayOfWeek = calendar == "Compensation"
            ? await GetDonorDayOfWeekAsync(date)
            : (int)date.DayOfWeek;

        if (dayOfWeek == 0)
            dayOfWeek = 7;

        var templateId = await GetActiveTemplateIdAsync(date);
        if (templateId is null)
            return (calendar != "Holiday", []);

        var lessons = await _weekTemplates.GetSlotsForTemplateDayAsync(templateId.Value, dayOfWeek);
        foreach (var lesson in lessons)
            lesson.Date = date;

        var dateStr = date.ToString("yyyy-MM-dd");
        var records = await _overrides.GetRecordsForDateAsync(dateStr);
        await ApplySlotOverridesAsync(lessons, records);
        await ApplyBellTimesAsync(lessons, date, records);
        await _availability.ApplyStaffAbsencesAsync(lessons, date);
        return (true, lessons);
    }

    private async Task<string?> GetCalendarTypeAsync(DateOnly date)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT exception_type FROM calendar_exceptions
            WHERE $d BETWEEN start_date AND COALESCE(end_date, start_date)
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$d", date.ToString("yyyy-MM-dd"));
        return await cmd.ExecuteScalarAsync() as string;
    }

    private async Task<int> GetDonorDayOfWeekAsync(DateOnly date)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT donor_day_of_week FROM calendar_exceptions WHERE start_date = $d LIMIT 1";
        cmd.Parameters.AddWithValue("$d", date.ToString("yyyy-MM-dd"));
        var result = await cmd.ExecuteScalarAsync();
        return result is null ? 1 : Convert.ToInt32(result);
    }

    private async Task<int?> GetActiveTemplateIdAsync(DateOnly date)
    {
        var periodEntries = await _periods.GetAllAsync();
        var period = SchedulePeriodResolver.ResolveMatchingPeriod(date, periodEntries);
        if (period is null)
            return await GetFallbackTemplateIdAsync();

        var parity = SchedulePeriodResolver.ResolveTemplateParity(period, date);
        var templates = await _weekTemplates.GetTemplatesAsync();
        var templateId = SchedulePeriodResolver.ResolveTemplateId(parity, templates);
        return templateId > 0 ? templateId : await GetFallbackTemplateIdAsync();
    }

    private async Task<int?> GetFallbackTemplateIdAsync()
    {
        var templates = await _weekTemplates.GetTemplatesAsync();
        if (templates.Count == 0)
            return null;

        var any = templates.FirstOrDefault(t => t.WeekParity == WeekTemplateParity.Any);
        return any?.Id ?? templates[0].Id;
    }

    private async Task ApplySlotOverridesAsync(List<LessonSlot> lessons, List<DayOverrideRecord> records)
    {
        var rooms = await _rooms.GetAllAsync();
        foreach (var record in records)
        {
            switch (record.OverrideType)
            {
                case "SwapSlots":
                    ApplySwapSlots(lessons, record);
                    break;
                case "MoveLesson":
                    ApplyMoveLesson(lessons, record);
                    break;
                case "CancelLesson":
                    ApplyCancelLesson(lessons, record);
                    break;
                case "ChangeSlot":
                    await ApplyChangeSlotAsync(lessons, record, rooms);
                    break;
                case "Substitution":
                    await ApplySubstitutionAsync(lessons, record);
                    break;
            }
        }
    }

    private static void ApplySwapSlots(List<LessonSlot> lessons, DayOverrideRecord record)
    {
        if (record.ClassId is not int classId || record.LessonNumber is not int lessonNumber
            || record.TargetClassId is not int targetClassId || record.TargetLessonNumber is not int targetLessonNumber)
            return;

        var a = FindLesson(lessons, classId, lessonNumber);
        var b = FindLesson(lessons, targetClassId, targetLessonNumber);
        if (a is not null && b is not null)
            SwapLessonContent(a, b);
    }

    private static void ApplyMoveLesson(List<LessonSlot> lessons, DayOverrideRecord record)
    {
        if (record.ClassId is not int classId || record.LessonNumber is not int lessonNumber
            || record.TargetLessonNumber is not int targetLessonNumber)
            return;

        var lesson = FindLesson(lessons, classId, lessonNumber);
        if (lesson is null)
            return;

        lesson.LessonNumber = targetLessonNumber;
        lesson.IsCancelled = false;
    }

    private static void ApplyCancelLesson(List<LessonSlot> lessons, DayOverrideRecord record)
    {
        if (record.ClassId is not int classId || record.LessonNumber is not int lessonNumber)
            return;

        foreach (var lesson in lessons.Where(l => l.ClassId == classId && l.LessonNumber == lessonNumber))
            lesson.IsCancelled = true;
    }

    private async Task ApplyChangeSlotAsync(
        List<LessonSlot> lessons,
        DayOverrideRecord record,
        IReadOnlyList<Room> rooms)
    {
        if (record.ClassId is not int classId || record.LessonNumber is not int lessonNumber)
            return;

        foreach (var lesson in lessons.Where(l =>
                     l.ClassId == classId && l.LessonNumber == lessonNumber && !l.IsCancelled))
        {
            if (record.TeacherId is int teacherId)
            {
                lesson.TeacherId = teacherId;
                lesson.TeacherName = await GetTeacherNameAsync(teacherId);
            }

            if (record.ClearRoom)
            {
                lesson.RoomId = 0;
                lesson.RoomNumber = "";
                lesson.BuildingName = "";
            }
            else if (record.RoomId is int roomId)
            {
                var room = rooms.FirstOrDefault(r => r.Id == roomId);
                if (room is not null)
                {
                    lesson.RoomId = room.Id;
                    lesson.RoomNumber = room.Number;
                    lesson.BuildingName = room.BuildingName;
                }
            }
        }
    }

    private async Task ApplySubstitutionAsync(List<LessonSlot> lessons, DayOverrideRecord record)
    {
        if (record.TeacherId is not int teacherId || record.LessonNumber is not int lessonNumber
            || record.ReplacementTeacherId is not int replacementTeacherId)
            return;

        var lesson = lessons.FirstOrDefault(l =>
            l.TeacherId == teacherId && l.LessonNumber == lessonNumber
            && (record.ClassId is null || l.ClassId == record.ClassId));
        if (lesson is null || lesson.IsCancelled)
            return;

        lesson.ReplacementTeacherId = replacementTeacherId;
        lesson.ReplacementTeacherName = await GetTeacherNameAsync(replacementTeacherId);
    }

    private async Task ApplyBellTimesAsync(List<LessonSlot> lessons, DateOnly date, List<DayOverrideRecord> records)
    {
        var periods = await _bells.GetAllPeriodsAsync();
        var dayBellOverride = GetDayBellTemplateOverride(records);
        var dayBellAdjustment = GetDayBellAdjustment(records);
        var classList = await _classes.GetAllAsync();
        var assignment = _bellAssignment.CreateSnapshot(classList, date);

        foreach (var lesson in lessons)
        {
            if (lesson.IsCancelled)
                continue;

            if (dayBellAdjustment?.SkipDynamicPause == true
                && SubjectScheduleRules.IsDynamicPause(lesson.SubjectName))
            {
                lesson.IsCancelled = true;
                continue;
            }

            var templateName = assignment.GetTemplateName(lesson.ClassId, lesson.ClassGrade, lesson.ClassShift);
            var period = ResolvePeriod(periods, lesson, dayBellOverride, dayBellAdjustment, templateName);
            BellScheduleResolver.ApplyLessonTimes(lesson, period);
        }

        if (dayBellOverride is not null || dayBellAdjustment is not null)
        {
            foreach (var lesson in lessons)
            {
                if (lesson.IsCancelled)
                    continue;

                var templateName = assignment.GetTemplateName(lesson.ClassId, lesson.ClassGrade, lesson.ClassShift);
                var period = ResolvePeriod(periods, lesson, dayBellOverride, dayBellAdjustment, templateName);
                if (period is null)
                    lesson.IsCancelled = true;
            }
        }
    }

    private static BellPeriod? ResolvePeriod(
        IReadOnlyList<BellPeriod> periods,
        LessonSlot lesson,
        string? dayBellOverride,
        DayBellAdjustment? dayBellAdjustment,
        string? templateName)
    {
        if (dayBellOverride is not null)
            return ScheduleGridBuilder.ResolvePeriodForSlot(periods, lesson, dayBellOverride);

        if (dayBellAdjustment is not null)
            return DayBellAdjuster.ResolveLessonPeriod(periods, lesson, dayBellAdjustment);

        return ScheduleGridBuilder.ResolvePeriodForSlot(periods, lesson, templateName);
    }

    private static string? GetDayBellTemplateOverride(IReadOnlyList<DayOverrideRecord> records)
    {
        var entry = records.LastOrDefault(r =>
            r.OverrideType == "ShortenedDay"
            && r.BellTemplateId is not null
            && DayBellAdjustment.TryParse(r.Note) is null);

        return entry?.Note;
    }

    private static DayBellAdjustment? GetDayBellAdjustment(IReadOnlyList<DayOverrideRecord> records)
    {
        var entry = records.LastOrDefault(r =>
            r.OverrideType == "ShortenedDay"
            && r.BellTemplateId is null
            && DayBellAdjustment.TryParse(r.Note) is not null);

        return entry is null ? null : DayBellAdjustment.TryParse(entry.Note);
    }

    private static LessonSlot? FindLesson(List<LessonSlot> lessons, int classId, int lessonNumber) =>
        lessons.FirstOrDefault(l => l.ClassId == classId && l.LessonNumber == lessonNumber && !l.IsCancelled)
        ?? lessons.FirstOrDefault(l => l.ClassId == classId && l.LessonNumber == lessonNumber);

    internal static void SwapLessonContent(LessonSlot a, LessonSlot b)
    {
        (a.SlotId, b.SlotId) = (b.SlotId, a.SlotId);
        (a.SubjectId, b.SubjectId) = (b.SubjectId, a.SubjectId);
        (a.SubjectName, b.SubjectName) = (b.SubjectName, a.SubjectName);
        (a.TeacherId, b.TeacherId) = (b.TeacherId, a.TeacherId);
        (a.TeacherName, b.TeacherName) = (b.TeacherName, a.TeacherName);
        (a.RoomId, b.RoomId) = (b.RoomId, a.RoomId);
        (a.RoomNumber, b.RoomNumber) = (b.RoomNumber, a.RoomNumber);
        (a.BuildingName, b.BuildingName) = (b.BuildingName, a.BuildingName);
        (a.SubgroupIndex, b.SubgroupIndex) = (b.SubgroupIndex, a.SubgroupIndex);
        (a.ReplacementTeacherId, b.ReplacementTeacherId) = (b.ReplacementTeacherId, a.ReplacementTeacherId);
        (a.ReplacementTeacherName, b.ReplacementTeacherName) = (b.ReplacementTeacherName, a.ReplacementTeacherName);
        (a.AbsenceNote, b.AbsenceNote) = (b.AbsenceNote, a.AbsenceNote);
    }

    private async Task<string> GetTeacherNameAsync(int teacherId)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT full_name FROM teachers WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", teacherId);
        return (await cmd.ExecuteScalarAsync() as string) ?? "";
    }
}
