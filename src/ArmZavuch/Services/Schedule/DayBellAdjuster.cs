using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>
/// Корректирует звонки на день: короче уроки, короче перемены, меньше уроков; пересчитывает время цепочкой.
/// Дин. пауза следует правилам уроков (длительность и сокращение), не перемен.
/// Вход: периоды шаблона класса и параметры. Выход: скорректированный период урока.
/// </summary>
public static class DayBellAdjuster
{
    private const int MinPeriodMinutes = 5;
    private const int DefaultMaxLessons = 8;
    private const int DefaultReplacementBreakMinutes = 10;

    /// <summary>Линейка столбцов сетки после суточной правки звонков (уроки, перемены, без дин. паузы при nopause).</summary>
    public static List<ConstructorTimelineColumn> BuildAdjustedTimeline(
        IReadOnlyList<BellPeriod> allPeriods,
        int classGrade,
        int classShift,
        DayBellAdjustment adjustment,
        string? templateName = null)
    {
        var usesPrimary = UsesPrimaryTimeline(allPeriods, classGrade, classShift);
        var templateTimeline = usesPrimary
            ? BellScheduleResolver.BuildPrimaryTimeline(
                allPeriods, classGrade, classShift, includeBreaks: true, templateName)
            : null;

        var entries = BuildTimelineEntries(allPeriods, classGrade, classShift, templateName);
        if (entries.Count == 0)
            return [];

        ApplyAdjustments(entries, adjustment);
        Recascade(entries);

        if (adjustment.SkipDynamicPause && templateTimeline is not null)
            return MapCompressedPrimaryColumns(entries, templateTimeline);

        if (templateTimeline is not null)
            return MapAdjustedPrimaryColumns(entries, templateTimeline);

        return entries.Select(ToTimelineColumn).ToList();
    }

    public static BellPeriod? ResolveLessonPeriod(
        IReadOnlyList<BellPeriod> allPeriods,
        LessonSlot slot,
        DayBellAdjustment adjustment)
    {
        if (adjustment.MaxLessons is int maxLessons && slot.LessonNumber > maxLessons)
            return null;

        var templateName = !string.IsNullOrWhiteSpace(slot.BellTemplateName)
            ? slot.BellTemplateName
            : BellScheduleResolver.FindLessonPeriod(
                allPeriods, slot.ClassGrade, slot.ClassShift, slot.LessonNumber)?.TemplateName;

        var entries = BuildTimelineEntries(allPeriods, slot.ClassGrade, slot.ClassShift, templateName);
        if (entries.Count == 0)
            return null;

        ApplyAdjustments(entries, adjustment);
        Recascade(entries);

        if (SubjectScheduleRules.IsDynamicPause(slot.SubjectName))
        {
            var timeline = BellScheduleResolver.BuildPrimaryTimeline(
                allPeriods, slot.ClassGrade, slot.ClassShift, templateName: templateName);
            foreach (var column in timeline.Where(c => c.IsDynamicPause))
            {
                if (ScheduleGridBuilder.ResolveTimelineStorageLessonNumber(column, timeline) != slot.LessonNumber)
                    continue;

                var pause = entries.FirstOrDefault(e =>
                    e.Kind == BellPeriodKinds.DynamicPause && e.AfterLessonNumber == column.AfterLessonNumber);
                return pause is null ? null : ToBellPeriod(pause, templateName);
            }

            return null;
        }

        var logical = ScheduleGridBuilder.ResolveLogicalLessonNumber(allPeriods, slot);
        var lesson = entries.FirstOrDefault(e =>
            e.Kind == BellPeriodKinds.Lesson && e.LessonNumber == logical);
        return lesson is null ? null : ToBellPeriod(lesson, templateName);
    }

    private static void ApplyAdjustments(List<TimelineEntry> entries, DayBellAdjustment adjustment)
    {
        if (adjustment.ShortLessonsMinutes is int lessonMinutes)
        {
            Trim(entries, BellPeriodKinds.Lesson, lessonMinutes);
            if (!adjustment.SkipDynamicPause)
                Trim(entries, BellPeriodKinds.DynamicPause, lessonMinutes);
        }

        if (adjustment.SkipDynamicPause)
        {
            ReplaceDynamicPausesWithBreaks(entries, adjustment);
        }
        else if (adjustment.ShortBreaksMinutes is int breakMinutes)
        {
            Trim(entries, BellPeriodKinds.Break, breakMinutes);
        }

        if (adjustment.FixedLessonsMinutes is int fixedLessons)
        {
            SetDuration(entries, BellPeriodKinds.Lesson, fixedLessons);
            if (!adjustment.SkipDynamicPause)
                SetDuration(entries, BellPeriodKinds.DynamicPause, fixedLessons);
        }

        if (adjustment.FixedBreaksMinutes is int fixedBreaks)
            SetDuration(entries, BellPeriodKinds.Break, fixedBreaks);

        if (adjustment.MaxLessons is int maxLessons)
        {
            entries.RemoveAll(e =>
                (e.Kind == BellPeriodKinds.Lesson && e.LessonNumber > maxLessons)
                || (e.Kind == BellPeriodKinds.Break && e.AfterLessonNumber > maxLessons)
                || (e.Kind == BellPeriodKinds.DynamicPause && e.AfterLessonNumber >= maxLessons));
        }
    }

    private static List<TimelineEntry> BuildTimelineEntries(
        IReadOnlyList<BellPeriod> allPeriods,
        int classGrade,
        int classShift,
        string? templateName)
    {
        var columns = UsesPrimaryTimeline(allPeriods, classGrade, classShift)
            ? BellScheduleResolver.BuildPrimaryTimeline(
                allPeriods, classGrade, classShift, includeBreaks: true, templateName)
            : BellScheduleResolver.BuildStandardLessonTimeline(
                BellScheduleResolver.ResolveShiftBellPeriods(allPeriods, classShift, templateName),
                BellScheduleResolver.ResolveBellLookupGrade(classGrade, templateName),
                classShift,
                DefaultMaxLessons,
                includeBreaks: true,
                templateName);

        var entries = new List<TimelineEntry>(columns.Count);
        foreach (var column in columns)
        {
            if (!TryParseDisplayRange(column.BellTimeDisplay, out var start, out var end))
                continue;

            entries.Add(new TimelineEntry
            {
                Kind = column.IsBreak
                    ? BellPeriodKinds.Break
                    : column.IsDynamicPause
                        ? BellPeriodKinds.DynamicPause
                        : BellPeriodKinds.Lesson,
                LessonNumber = column.LessonNumber,
                AfterLessonNumber = column.AfterLessonNumber,
                Start = start,
                End = end
            });
        }

        return entries.OrderBy(e => e.Start).ThenBy(e => e.LessonNumber).ToList();
    }

    private static bool UsesPrimaryTimeline(IReadOnlyList<BellPeriod> allPeriods, int classGrade, int classShift) =>
        classGrade is >= 1 and <= 4
        && BellScheduleResolver.GetDynamicPausesForGrade(allPeriods, classGrade, classShift).Count > 0;

    private static void Trim(List<TimelineEntry> entries, string kind, int minutes)
    {
        foreach (var entry in entries.Where(e => e.Kind == kind))
        {
            var duration = Math.Max(MinPeriodMinutes, (int)(entry.End - entry.Start).TotalMinutes - minutes);
            entry.End = entry.Start.Add(TimeSpan.FromMinutes(duration));
        }
    }

    private static void SetDuration(List<TimelineEntry> entries, string kind, int minutes)
    {
        var duration = Math.Max(MinPeriodMinutes, minutes);
        foreach (var entry in entries.Where(e => e.Kind == kind))
            entry.End = entry.Start.Add(TimeSpan.FromMinutes(duration));
    }

    private static void ReplaceDynamicPausesWithBreaks(
        List<TimelineEntry> entries,
        DayBellAdjustment adjustment)
    {
        var duration = ResolveReplacementBreakMinutes(adjustment);

        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].Kind != BellPeriodKinds.DynamicPause)
                continue;

            var pause = entries[i];
            var hasAdjacentBreak = (i > 0 && entries[i - 1].Kind == BellPeriodKinds.Break)
                || (i + 1 < entries.Count && entries[i + 1].Kind == BellPeriodKinds.Break);

            if (hasAdjacentBreak)
            {
                entries.RemoveAt(i);
                i--;
                continue;
            }

            entries[i] = new TimelineEntry
            {
                Kind = BellPeriodKinds.Break,
                AfterLessonNumber = pause.AfterLessonNumber,
                LessonNumber = 0,
                Start = pause.Start,
                End = pause.Start.Add(TimeSpan.FromMinutes(duration))
            };
        }
    }

    private static int ResolveReplacementBreakMinutes(DayBellAdjustment adjustment)
    {
        if (adjustment.FixedBreaksMinutes is int fixedMinutes)
            return Math.Max(MinPeriodMinutes, fixedMinutes);

        var minutes = DefaultReplacementBreakMinutes;
        if (adjustment.ShortBreaksMinutes is int trim)
            minutes = Math.Max(MinPeriodMinutes, minutes - trim);

        return minutes;
    }

    private static void Recascade(List<TimelineEntry> entries)
    {
        for (var i = 1; i < entries.Count; i++)
        {
            var duration = Math.Max(MinPeriodMinutes, (int)(entries[i].End - entries[i].Start).TotalMinutes);
            entries[i].Start = entries[i - 1].End;
            entries[i].End = entries[i].Start.Add(TimeSpan.FromMinutes(duration));
        }
    }

    private static List<ConstructorTimelineColumn> MapCompressedPrimaryColumns(
        IReadOnlyList<TimelineEntry> entries,
        IReadOnlyList<ConstructorTimelineColumn> templateTimeline)
    {
        var result = new List<ConstructorTimelineColumn>(entries.Count);
        var displayLesson = 0;

        foreach (var entry in entries)
        {
            var timeDisplay = $"{FormatClock(entry.Start)}–{FormatClock(entry.End)}";
            if (entry.Kind == BellPeriodKinds.Break)
            {
                result.Add(new ConstructorTimelineColumn
                {
                    IsBreak = true,
                    AfterLessonNumber = entry.AfterLessonNumber,
                    Title = "Перемена",
                    BellTimeDisplay = timeDisplay
                });
                continue;
            }

            if (entry.Kind == BellPeriodKinds.DynamicPause)
                continue;

            displayLesson++;
            var templateColumn = templateTimeline.First(c =>
                !c.IsBreak && !c.IsDynamicPause && c.LessonNumber == entry.LessonNumber);
            var storage = ScheduleGridBuilder.ResolveTimelineStorageLessonNumber(templateColumn, templateTimeline);

            result.Add(new ConstructorTimelineColumn
            {
                LessonNumber = displayLesson,
                BellLessonNumber = templateColumn.BellLessonNumber > 0
                    ? templateColumn.BellLessonNumber
                    : templateColumn.LessonNumber,
                StorageLessonNumber = storage,
                Title = $"Урок {displayLesson}",
                BellTimeDisplay = timeDisplay
            });
        }

        return result;
    }

    private static List<ConstructorTimelineColumn> MapAdjustedPrimaryColumns(
        IReadOnlyList<TimelineEntry> entries,
        IReadOnlyList<ConstructorTimelineColumn> templateTimeline)
    {
        var result = new List<ConstructorTimelineColumn>(entries.Count);

        foreach (var entry in entries)
        {
            var timeDisplay = $"{FormatClock(entry.Start)}–{FormatClock(entry.End)}";
            if (entry.Kind == BellPeriodKinds.Break)
            {
                result.Add(new ConstructorTimelineColumn
                {
                    IsBreak = true,
                    AfterLessonNumber = entry.AfterLessonNumber,
                    Title = "Перемена",
                    BellTimeDisplay = timeDisplay
                });
                continue;
            }

            if (entry.Kind == BellPeriodKinds.DynamicPause)
            {
                var templatePause = templateTimeline.First(c =>
                    c.IsDynamicPause && c.AfterLessonNumber == entry.AfterLessonNumber);
                result.Add(new ConstructorTimelineColumn
                {
                    IsDynamicPause = true,
                    AfterLessonNumber = entry.AfterLessonNumber,
                    StorageLessonNumber = ScheduleGridBuilder.ResolveTimelineStorageLessonNumber(
                        templatePause, templateTimeline),
                    Title = "Дин. пауза",
                    BellTimeDisplay = timeDisplay
                });
                continue;
            }

            var templateColumn = templateTimeline.First(c =>
                !c.IsBreak && !c.IsDynamicPause && c.LessonNumber == entry.LessonNumber);
            result.Add(new ConstructorTimelineColumn
            {
                LessonNumber = entry.LessonNumber,
                BellLessonNumber = templateColumn.BellLessonNumber > 0
                    ? templateColumn.BellLessonNumber
                    : templateColumn.LessonNumber,
                StorageLessonNumber = ScheduleGridBuilder.ResolveTimelineStorageLessonNumber(
                    templateColumn, templateTimeline),
                Title = $"Урок {entry.LessonNumber}",
                BellTimeDisplay = timeDisplay
            });
        }

        return result;
    }

    private static ConstructorTimelineColumn ToTimelineColumn(TimelineEntry entry)
    {
        var timeDisplay = $"{FormatClock(entry.Start)}–{FormatClock(entry.End)}";
        if (entry.Kind == BellPeriodKinds.Break)
        {
            return new ConstructorTimelineColumn
            {
                IsBreak = true,
                AfterLessonNumber = entry.AfterLessonNumber,
                Title = "Перемена",
                BellTimeDisplay = timeDisplay
            };
        }

        if (entry.Kind == BellPeriodKinds.DynamicPause)
        {
            return new ConstructorTimelineColumn
            {
                IsDynamicPause = true,
                AfterLessonNumber = entry.AfterLessonNumber,
                StorageLessonNumber = entry.AfterLessonNumber + 1,
                Title = "Дин. пауза",
                BellTimeDisplay = timeDisplay
            };
        }

        return new ConstructorTimelineColumn
        {
            LessonNumber = entry.LessonNumber,
            BellLessonNumber = entry.LessonNumber,
            StorageLessonNumber = entry.LessonNumber,
            Title = $"Урок {entry.LessonNumber}",
            BellTimeDisplay = timeDisplay
        };
    }

    private static BellPeriod ToBellPeriod(TimelineEntry entry, string? templateName) => new()
    {
        TemplateName = templateName ?? "",
        LessonNumber = entry.LessonNumber,
        PeriodKind = entry.Kind,
        StartTime = FormatClock(entry.Start),
        EndTime = FormatClock(entry.End)
    };

    private static bool TryParseDisplayRange(string? display, out TimeSpan start, out TimeSpan end)
    {
        start = default;
        end = default;
        if (string.IsNullOrWhiteSpace(display))
            return false;

        var parts = display.Split(new[] { '–', '—', '-' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return false;

        return TryParseClock(parts[0], out start) && TryParseClock(parts[1], out end) && end > start;
    }

    private static bool TryParseClock(string value, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        if (TimeSpan.TryParse(value, out time))
            return true;
        if (TimeOnly.TryParse(value, out var clock))
        {
            time = clock.ToTimeSpan();
            return true;
        }

        return false;
    }

    private static string FormatClock(TimeSpan time) =>
        TimeOnly.FromTimeSpan(time).ToString("HH:mm");

    private sealed class TimelineEntry
    {
        public required string Kind { get; init; }
        public int LessonNumber { get; init; }
        public int AfterLessonNumber { get; init; }
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
    }
}
