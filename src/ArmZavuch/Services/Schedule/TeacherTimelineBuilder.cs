using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>Timeline уроков педагога с дин. паузами (начальная школа).</summary>
public static class TeacherTimelineBuilder
{
    public static bool UsesPrimaryTimeline(
        int teacherId,
        IReadOnlyList<LessonSlot> slots,
        IReadOnlyList<BellPeriod> bells)
    {
        var teacherSlots = slots.Where(s => s.TeacherId == teacherId).ToList();
        if (teacherSlots.Any(s => SubjectScheduleRules.IsDynamicPause(s.SubjectName)))
            return true;

        return teacherSlots.Any(s => s.ClassGrade is >= 1 and <= 4
            && BellScheduleResolver.GetDynamicPausesForGrade(bells, s.ClassGrade, s.ClassShift).Count > 0);
    }

    public static List<ConstructorTimelineColumn> BuildColumns(
        int teacherId,
        IReadOnlyList<LessonSlot> allSlots,
        IReadOnlyList<BellPeriod> bells,
        int maxLessons = ScheduleGridBuilder.DefaultMaxLessons,
        bool includeBreaks = false,
        BellTemplateAssignmentSnapshot? assignment = null)
    {
        assignment ??= BellTemplateAssignmentSnapshot.Fallback;
        var teacherSlots = allSlots.Where(s => s.TeacherId == teacherId).ToList();
        if (teacherSlots.Count == 0)
            return [];

        if (!UsesPrimaryTimeline(teacherId, allSlots, bells))
            return BuildStandardColumns(teacherSlots, bells, maxLessons, includeBreaks, assignment);

        var (grade, shift) = ResolveProfile(teacherSlots);
        string? templateName = null;
        if (grade == ScheduleGridBuilder.FirstGradeTimelineGrade)
        {
            templateName = teacherSlots
                .Where(s => s.ClassGrade == grade && !string.IsNullOrWhiteSpace(s.BellTemplateName))
                .Select(s => s.BellTemplateName)
                .FirstOrDefault()
                ?? assignment.GetTemplateName(
                    teacherSlots.First(s => s.ClassGrade == grade).ClassId, grade, shift);
        }

        var columns = BellScheduleResolver.BuildPrimaryTimeline(
            bells, grade, shift, includeBreaks, templateName).ToList();

        var seniorLessonNumbers = teacherSlots
            .Where(s => s.ClassGrade >= 5 && !SubjectScheduleRules.IsDynamicPause(s.SubjectName))
            .Select(s => s.LessonNumber)
            .Distinct()
            .OrderBy(n => n);

        foreach (var lessonNumber in seniorLessonNumbers)
        {
            if (columns.Any(c => !c.IsDynamicPause && !c.IsBreak && c.LessonNumber == lessonNumber))
                continue;

            var sample = teacherSlots.First(s => s.ClassGrade >= 5 && s.LessonNumber == lessonNumber);
            var period = BellScheduleResolver.FindLessonPeriod(bells, sample.ClassGrade, sample.ClassShift, lessonNumber);
            columns.Add(new ConstructorTimelineColumn
            {
                LessonNumber = lessonNumber,
                StorageLessonNumber = lessonNumber,
                Title = $"Урок {lessonNumber}",
                BellTimeDisplay = BellScheduleResolver.FormatPeriodTime(period)
            });
        }

        return columns;
    }

    private static List<ConstructorTimelineColumn> BuildStandardColumns(
        IReadOnlyList<LessonSlot> teacherSlots,
        IReadOnlyList<BellPeriod> bells,
        int maxLessons,
        bool includeBreaks,
        BellTemplateAssignmentSnapshot assignment)
    {
        var merged = new List<ConstructorTimelineColumn>();
        foreach (var shift in teacherSlots.Select(s => s.ClassShift).Distinct().OrderBy(s => s))
        {
            var templateName = assignment.ResolveShiftStandardTemplateName(shift);
            var templatePeriods = BellScheduleResolver.ResolveShiftBellPeriods(bells, shift, templateName);
            var timeline = BellScheduleResolver.BuildStandardLessonTimeline(
                templatePeriods,
                BellScheduleResolver.StandardBellLookupGrade,
                shift,
                maxLessons,
                includeBreaks,
                templateName);
            foreach (var column in timeline)
            {
                if (merged.Any(existing => ColumnsMatch(existing, column)))
                    continue;
                merged.Add(column);
            }
        }

        return merged
            .OrderBy(c => ColumnSortMinutes(c, bells, teacherSlots))
            .ThenBy(c => c.LessonNumber)
            .ToList();
    }

    private static bool ColumnsMatch(ConstructorTimelineColumn left, ConstructorTimelineColumn right)
    {
        if (left.IsBreak != right.IsBreak || left.IsDynamicPause != right.IsDynamicPause)
            return false;

        return left.IsBreak || left.IsDynamicPause
            ? left.AfterLessonNumber == right.AfterLessonNumber
            : left.LessonNumber == right.LessonNumber;
    }

    private static int ColumnSortMinutes(
        ConstructorTimelineColumn column,
        IReadOnlyList<BellPeriod> bells,
        IReadOnlyList<LessonSlot> slots)
    {
        var sample = slots.FirstOrDefault(s => s.ClassGrade is >= 1 and <= 11) ?? slots[0];
        var period = column.IsDynamicPause
            ? BellScheduleResolver.FindDynamicPauseAfterLesson(bells, sample.ClassGrade, sample.ClassShift, column.AfterLessonNumber)
            : column.IsBreak
                ? BellScheduleResolver.GetBreaksForGrade(bells, sample.ClassGrade, sample.ClassShift)
                    .FirstOrDefault(b => b.LessonNumber == column.AfterLessonNumber)
                : BellScheduleResolver.FindLessonPeriod(bells, sample.ClassGrade, sample.ClassShift, column.LessonNumber);

        if (period is not null && TimeSpan.TryParse(period.StartTime, out var start))
            return (int)start.TotalMinutes;

        return column.LessonNumber * 100;
    }

    public static List<LessonSlot> BuildTeacherDayDisplay(
        int teacherId,
        IReadOnlyList<LessonSlot> dayLessons,
        IReadOnlyList<BellPeriod> bells)
    {
        var teacherSlots = dayLessons
            .Where(l => l.TeacherId == teacherId && !l.IsCancelled)
            .ToList();
        if (teacherSlots.Count == 0)
            return [];

        foreach (var slot in teacherSlots)
        {
            slot.DisplayRowTitle = "";
            slot.DisplayLessonNumber = 0;
        }

        if (!UsesPrimaryTimeline(teacherId, dayLessons, bells))
        {
            foreach (var slot in teacherSlots.OrderBy(s => s.LessonNumber))
                ApplyPlainDisplay(slot, bells);
            return teacherSlots;
        }

        var timeline = BuildColumns(teacherId, dayLessons, bells);
        var lookup = teacherSlots
            .GroupBy(s => (TeacherId: teacherId, s.DayOfWeek, s.LessonNumber))
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SubgroupIndex).ToList());

        var used = new HashSet<int>();
        var result = new List<LessonSlot>(teacherSlots.Count);
        var day = teacherSlots[0].DayOfWeek;

        foreach (var column in timeline)
        {
            if (column.IsBreak)
                continue;

            var group = ScheduleGridBuilder.ResolveTeacherTimelineSlots(
                teacherId, day, column, timeline, lookup);
            if (group is null || group.Count == 0)
                continue;

            foreach (var slot in group)
            {
                if (!used.Add(slot.SlotId))
                    continue;

                ApplyTimelineDisplay(slot, column);
                result.Add(slot);
            }
        }

        foreach (var orphan in teacherSlots
                     .Where(s => !used.Contains(s.SlotId))
                     .OrderBy(s => ScheduleGridBuilder.ResolveLogicalLessonNumber(bells, s))
                     .ThenBy(s => s.LessonNumber))
        {
            ApplyPlainDisplay(orphan, bells);
            result.Add(orphan);
        }

        return result;
    }

    private static (int Grade, int Shift) ResolveProfile(IReadOnlyList<LessonSlot> teacherSlots)
    {
        var primary = teacherSlots.Where(s => s.ClassGrade is >= 1 and <= 4).ToList();
        if (primary.Count == 0)
            primary = teacherSlots.ToList();

        var grade = primary.Any(s => s.ClassGrade == ScheduleGridBuilder.FirstGradeTimelineGrade)
            ? ScheduleGridBuilder.FirstGradeTimelineGrade
            : primary.Where(s => s.ClassGrade is >= 1 and <= 4).Select(s => s.ClassGrade).DefaultIfEmpty(5).Min();

        var shift = primary
            .GroupBy(s => s.ClassShift)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .First().Key;

        return (grade, shift);
    }

    private static void ApplyTimelineDisplay(LessonSlot slot, ConstructorTimelineColumn column)
    {
        if (column.IsDynamicPause || SubjectScheduleRules.IsDynamicPause(slot.SubjectName))
        {
            slot.DisplayRowTitle = "Дин. пауза";
            slot.DisplayLessonNumber = 0;
            return;
        }

        slot.DisplayRowTitle = column.LessonNumber.ToString();
        slot.DisplayLessonNumber = column.LessonNumber;
    }

    private static void ApplyPlainDisplay(LessonSlot slot, IReadOnlyList<BellPeriod> bells)
    {
        if (SubjectScheduleRules.IsDynamicPause(slot.SubjectName))
        {
            slot.DisplayRowTitle = "Дин. пауза";
            slot.DisplayLessonNumber = 0;
            return;
        }

        var logical = ScheduleGridBuilder.ResolveLogicalLessonNumber(bells, slot);
        slot.DisplayLessonNumber = logical;
        slot.DisplayRowTitle = logical.ToString();
    }
}
