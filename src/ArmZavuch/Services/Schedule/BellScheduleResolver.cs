using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>
/// Подбор звонков по параллели класса: 1 класс, 2–4, 5–11 — параллельные «дорожки» без конфликта номеров уроков.
/// Стандартная сетка: перемены из справочника или из промежутка между соседними уроками.
/// </summary>
public static class BellScheduleResolver
{
    public static IReadOnlyList<BellPeriod> FilterByTemplate(
        IReadOnlyList<BellPeriod> allPeriods,
        string? templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName))
            return allPeriods;

        var exact = allPeriods
            .Where(p => p.TemplateName.Equals(templateName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (exact.Count > 0)
            return exact;

        if (templateName.Contains("2 смена", StringComparison.OrdinalIgnoreCase))
        {
            return allPeriods
                .Where(p => p.TemplateName.Contains("2 смена", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (templateName.Contains("Стандарт", StringComparison.OrdinalIgnoreCase)
            || templateName.Contains("1 смена", StringComparison.OrdinalIgnoreCase))
        {
            return allPeriods
                .Where(p => p.TemplateName.Contains("Стандарт", StringComparison.OrdinalIgnoreCase)
                            || p.TemplateName.Contains("1 смена", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return [];
    }

    /// <summary>Звонки 2–11 классов: один шаблон на смену (1 или 2).</summary>
    public static IReadOnlyList<BellPeriod> ResolveShiftBellPeriods(
        IReadOnlyList<BellPeriod> allPeriods,
        int shift,
        string? preferredTemplateName = null)
    {
        foreach (var name in EnumerateShiftTemplateNames(shift, preferredTemplateName))
        {
            var filtered = FilterByTemplate(allPeriods, name);
            if (filtered.Count > 0)
                return filtered;
        }

        return allPeriods
            .Where(p => p.Shift == shift && p.MatchesGrade(StandardBellLookupGrade))
            .ToList();
    }

    public const int StandardBellLookupGrade = 5;

    public static BellPeriod? FindLessonPeriod(
        IReadOnlyList<BellPeriod> allPeriods,
        int classGrade,
        int classShift,
        int lessonNumber,
        string? overrideTemplateName = null)
    {
        var lessons = allPeriods
            .Where(p => BellPeriodKinds.IsLesson(p.PeriodKind))
            .Where(p => p.Shift == classShift && p.LessonNumber == lessonNumber)
            .ToList();

        if (lessons.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(overrideTemplateName))
        {
            var byOverride = lessons.FirstOrDefault(p =>
                p.TemplateName.Equals(overrideTemplateName, StringComparison.OrdinalIgnoreCase));
            if (byOverride is not null)
                return byOverride;
        }

        var byGrade = lessons
            .Where(p => p.MatchesGrade(classGrade))
            .OrderBy(p => p.GradeRangeWidth)
            .ThenBy(p => p.TemplateName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (byGrade.Count > 0)
            return byGrade[0];

        if (classGrade is >= 2 and <= 4)
        {
            var primary = lessons.FirstOrDefault(p =>
                              p.TemplateName.Contains("Начальная", StringComparison.OrdinalIgnoreCase))
                          ?? lessons.FirstOrDefault(p =>
                              p.TemplateGradeFrom <= classGrade && p.TemplateGradeTo >= classGrade);
            if (primary is not null)
                return primary;
        }

        if (classGrade >= 5)
        {
            return lessons.FirstOrDefault(p =>
                       p.TemplateName.Contains("Стандарт", StringComparison.OrdinalIgnoreCase))
                   ?? lessons.FirstOrDefault(p => p.TemplateGradeFrom >= 5);
        }

        if (classGrade >= 2)
            return ResolveStandardShiftLesson(lessons);

        return null;
    }

    private static BellPeriod? ResolveStandardShiftLesson(IReadOnlyList<BellPeriod> lessons) =>
        lessons.FirstOrDefault(p => p.MatchesGrade(StandardBellLookupGrade) && p.TemplateGradeFrom >= 5)
        ?? lessons.FirstOrDefault(p => p.TemplateGradeFrom >= 5);

    private static IEnumerable<string> EnumerateShiftTemplateNames(int shift, string? preferredTemplateName)
    {
        if (!string.IsNullOrWhiteSpace(preferredTemplateName))
            yield return preferredTemplateName.Trim();

        yield return shift == 2 ? BellTemplateNaming.SecondShift : BellTemplateNaming.Standard;
        yield return shift == 2 ? "2 смена" : "Стандарт";
    }

    public static string FormatPeriodTime(BellPeriod? period)
    {
        if (period is null || string.IsNullOrWhiteSpace(period.StartTime))
            return "";

        return string.IsNullOrWhiteSpace(period.EndTime)
            ? period.StartTime
            : $"{period.StartTime}–{period.EndTime}";
    }

    public static string GetLessonBellTime(
        IReadOnlyList<BellPeriod> allPeriods,
        int classGrade,
        int classShift,
        int lessonNumber,
        string? templateName = null)
        => FormatPeriodTime(FindLessonPeriod(allPeriods, classGrade, classShift, lessonNumber, templateName));

    public static List<LessonNumberHeader> BuildLessonHeaders(
        IReadOnlyList<BellPeriod> allPeriods,
        int classGrade,
        int classShift,
        int maxLessons)
    {
        var headers = new List<LessonNumberHeader>(maxLessons);
        for (var lesson = 1; lesson <= maxLessons; lesson++)
        {
            headers.Add(new LessonNumberHeader
            {
                LessonNumber = lesson,
                BellTimeDisplay = GetLessonBellTime(allPeriods, classGrade, classShift, lesson)
            });
        }

        return headers;
    }

    public static int ResolveBellLookupGrade(int classGrade, string? templateName = null)
    {
        if (string.IsNullOrWhiteSpace(templateName))
            return classGrade;

        if (templateName.Equals(BellTemplateNaming.Grade1, StringComparison.OrdinalIgnoreCase)
            || templateName.Equals(BellTemplateNaming.Grade1SecondHalf, StringComparison.OrdinalIgnoreCase))
            return Math.Max(1, classGrade);

        if (templateName.Equals(BellTemplateNaming.Primary, StringComparison.OrdinalIgnoreCase)
            || templateName.Contains("Начальная", StringComparison.OrdinalIgnoreCase))
            return Math.Clamp(classGrade, 2, 5);

        return Math.Max(StandardBellLookupGrade, classGrade);
    }

    public static int ResolveProfileGradeForTemplate(IEnumerable<int> classGrades, string templateName)
    {
        var min = classGrades.DefaultIfEmpty(5).Min();
        return ResolveBellLookupGrade(min, templateName);
    }

    /// <summary>Линейка столбцов для начальной школы: уроки и дин. паузы между ними по шаблону звонков.</summary>
    public static List<ConstructorTimelineColumn> BuildPrimaryTimeline(
        IReadOnlyList<BellPeriod> allPeriods,
        int classGrade,
        int classShift,
        bool includeBreaks = false,
        string? templateName = null)
    {
        var scoped = ScopeGradePeriods(allPeriods, classGrade, classShift, templateName);

        var lessons = scoped
            .Where(p => BellPeriodKinds.IsLesson(p.PeriodKind))
            .OrderBy(p => TimeSpan.TryParse(p.StartTime, out var ts) ? ts : TimeSpan.MaxValue)
            .ThenBy(p => p.LessonNumber)
            .ToList();

        var pauses = GetDynamicPausesForGrade(scoped, classGrade, classShift)
            .GroupBy(p => p.LessonNumber)
            .ToDictionary(g => g.Key, g => g.First());

        var breaks = includeBreaks
            ? GetBreaksForGrade(scoped, classGrade, classShift)
            : [];

        var columns = new List<ConstructorTimelineColumn>(lessons.Count + pauses.Count + breaks.Count);
        var displayLesson = 0;
        var dbStorageOffsetAfterPause = false;

        foreach (var lesson in lessons)
        {
            if (IsPhantomLessonSlot(lesson, pauses))
                continue;

            displayLesson++;
            columns.Add(new ConstructorTimelineColumn
            {
                IsDynamicPause = false,
                LessonNumber = displayLesson,
                BellLessonNumber = lesson.LessonNumber,
                StorageLessonNumber = dbStorageOffsetAfterPause
                    ? lesson.LessonNumber + 1
                    : lesson.LessonNumber,
                Title = $"Урок {displayLesson}",
                BellTimeDisplay = FormatPeriodTime(lesson)
            });

            if (includeBreaks)
            {
                foreach (var breakPeriod in SelectBreaksAfterLesson(breaks, lesson.LessonNumber))
                {
                    columns.Add(new ConstructorTimelineColumn
                    {
                        IsBreak = true,
                        AfterLessonNumber = displayLesson,
                        Title = "Перемена",
                        BellTimeDisplay = FormatPeriodTime(breakPeriod)
                    });
                }
            }

            if (!pauses.TryGetValue(lesson.LessonNumber, out var pause))
                continue;

            var phantomAtPauseSlot = lessons.Any(l =>
                l.LessonNumber == lesson.LessonNumber + 1 && PeriodsOverlap(l, pause));

            columns.Add(new ConstructorTimelineColumn
            {
                IsDynamicPause = true,
                AfterLessonNumber = displayLesson,
                StorageLessonNumber = lesson.LessonNumber + 1,
                Title = "Дин. пауза",
                BellTimeDisplay = FormatPeriodTime(pause)
            });

            dbStorageOffsetAfterPause = !phantomAtPauseSlot;
        }

        return columns;
    }

    private static IReadOnlyList<BellPeriod> ScopeGradePeriods(
        IReadOnlyList<BellPeriod> allPeriods,
        int classGrade,
        int classShift,
        string? templateName)
    {
        var scoped = string.IsNullOrWhiteSpace(templateName)
            ? allPeriods
            : FilterByTemplate(allPeriods, templateName);

        var filtered = scoped
            .Where(p => p.Shift == classShift && p.MatchesGrade(classGrade))
            .ToList();

        var distinctTemplates = filtered
            .Select(p => p.TemplateName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        if (!string.IsNullOrWhiteSpace(templateName))
            return filtered;

        if (distinctTemplates <= 1)
            return filtered;

        var dominantTemplate = filtered
            .GroupBy(p => p.TemplateName, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .First().Key;

        return filtered
            .Where(p => p.TemplateName.Equals(dominantTemplate, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>Урок в звонках на слоте паузы с тем же интервалом — не отдельная колонка сетки.</summary>
    private static bool IsPhantomLessonSlot(
        BellPeriod lesson,
        IReadOnlyDictionary<int, BellPeriod> pausesByAfterLesson)
    {
        if (!pausesByAfterLesson.TryGetValue(lesson.LessonNumber - 1, out var pause))
            return false;

        return PeriodsOverlap(lesson, pause);
    }

    public static bool PeriodsOverlap(BellPeriod a, BellPeriod b)
    {
        if (!TryParseTimeRange(a.StartTime, a.EndTime, out var aStart, out var aEnd)
            || !TryParseTimeRange(b.StartTime, b.EndTime, out var bStart, out var bEnd))
            return false;

        return aStart < bEnd && bStart < aEnd;
    }

    public static List<ConstructorTimelineColumn> BuildStandardLessonTimeline(
        IReadOnlyList<BellPeriod> allPeriods,
        int classGrade,
        int classShift,
        int maxLessons,
        bool includeBreaks = false,
        string? templateName = null)
    {
        var lookupGrade = ResolveBellLookupGrade(classGrade, templateName);
        var breaks = includeBreaks
            ? GetBreaksForGrade(allPeriods, lookupGrade, classShift)
            : [];

        var columns = new List<ConstructorTimelineColumn>(maxLessons * (includeBreaks ? 2 : 1));
        for (var lesson = 1; lesson <= maxLessons; lesson++)
        {
            var period = FindLessonPeriod(allPeriods, lookupGrade, classShift, lesson, templateName);
            columns.Add(new ConstructorTimelineColumn
            {
                LessonNumber = lesson,
                BellLessonNumber = lesson,
                StorageLessonNumber = lesson,
                Title = $"Урок {lesson}",
                BellTimeDisplay = FormatPeriodTime(period)
            });

            if (!includeBreaks)
                continue;

            var addedBreak = false;
            foreach (var breakPeriod in SelectBreaksAfterLesson(breaks, lesson))
            {
                addedBreak = true;
                columns.Add(new ConstructorTimelineColumn
                {
                    IsBreak = true,
                    AfterLessonNumber = lesson,
                    Title = "Перемена",
                    BellTimeDisplay = FormatPeriodTime(breakPeriod)
                });
            }

            if (!addedBreak
                && TryFormatInferredBreak(allPeriods, lookupGrade, classShift, lesson, templateName, out var inferredBreakTime))
            {
                columns.Add(new ConstructorTimelineColumn
                {
                    IsBreak = true,
                    AfterLessonNumber = lesson,
                    Title = "Перемена",
                    BellTimeDisplay = inferredBreakTime
                });
            }
        }

        return columns;
    }

    private static bool TryFormatInferredBreak(
        IReadOnlyList<BellPeriod> allPeriods,
        int classGrade,
        int classShift,
        int afterLesson,
        string? templateName,
        out string timeDisplay)
    {
        timeDisplay = "";
        var current = FindLessonPeriod(allPeriods, classGrade, classShift, afterLesson, templateName);
        var next = FindLessonPeriod(allPeriods, classGrade, classShift, afterLesson + 1, templateName);
        if (current is null || next is null)
            return false;

        if (!TryParseClock(current.EndTime, out var endTs) || !TryParseClock(next.StartTime, out var startTs))
            return false;

        if (startTs <= endTs)
            return false;

        timeDisplay = $"{current.EndTime}–{next.StartTime}";
        return true;
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

    public static IReadOnlyList<BellPeriod> GetBreaksForGrade(
        IReadOnlyList<BellPeriod> allPeriods,
        int classGrade,
        int classShift)
    {
        return allPeriods
            .Where(p => p.PeriodKind == BellPeriodKinds.Break)
            .Where(p => p.Shift == classShift && p.MatchesGrade(classGrade))
            .OrderBy(p => p.LessonNumber)
            .ThenBy(p => p.StartTime, StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<BellPeriod> SelectBreaksAfterLesson(
        IReadOnlyList<BellPeriod> breaks,
        int lessonNumber)
    {
        foreach (var breakPeriod in breaks)
        {
            var afterLesson = breakPeriod.LessonNumber <= 0 ? 1 : breakPeriod.LessonNumber;
            if (afterLesson == lessonNumber)
                yield return breakPeriod;
        }
    }

    public static IReadOnlyList<BellPeriod> GetDynamicPausesForGrade(
        IReadOnlyList<BellPeriod> allPeriods,
        int classGrade,
        int classShift)
    {
        return allPeriods
            .Where(p => p.PeriodKind == BellPeriodKinds.DynamicPause)
            .Where(p => p.Shift == classShift && p.MatchesGrade(classGrade))
            .OrderBy(p => p.LessonNumber)
            .ThenBy(p => p.StartTime, StringComparer.Ordinal)
            .ToList();
    }

    public static BellPeriod? FindDynamicPauseAfterLesson(
        IReadOnlyList<BellPeriod> allPeriods,
        int classGrade,
        int classShift,
        int afterLessonNumber)
        => GetDynamicPausesForGrade(allPeriods, classGrade, classShift)
            .FirstOrDefault(p => p.LessonNumber == afterLessonNumber);

    public static void ApplyLessonTimes(LessonSlot lesson, BellPeriod? period)
    {
        if (period is null)
            return;
        lesson.StartTime = period.StartTime;
        lesson.EndTime = period.EndTime;
        lesson.BellTemplateName = period.TemplateName;
    }

    public static bool TryGetLessonStart(LessonSlot lesson, out TimeSpan start) =>
        TryParseClock(lesson.StartTime, out start);

    public static bool TryGetLessonEnd(LessonSlot lesson, out TimeSpan end) =>
        TryParseClock(lesson.EndTime, out end);

    public static TimeSpan? ResolveLessonStartTime(LessonSlot lesson, IReadOnlyList<BellPeriod> allPeriods)
    {
        if (TryGetLessonStart(lesson, out var start))
            return start;

        var period = FindLessonPeriod(
            allPeriods,
            lesson.ClassGrade,
            lesson.ClassShift,
            lesson.LessonNumber,
            lesson.BellTemplateName);
        return period is not null && TryParseClock(period.StartTime, out var bellStart)
            ? bellStart
            : null;
    }

    public static TimeSpan? ResolveLessonEndTime(LessonSlot lesson, IReadOnlyList<BellPeriod> allPeriods)
    {
        if (TryGetLessonEnd(lesson, out var end))
            return end;

        var period = FindLessonPeriod(
            allPeriods,
            lesson.ClassGrade,
            lesson.ClassShift,
            lesson.LessonNumber,
            lesson.BellTemplateName);
        return period is not null && TryParseClock(period.EndTime, out var bellEnd)
            ? bellEnd
            : null;
    }

    /// <summary>Сколько уроков смены начинается в интервале (afterEnd; beforeStart), по школьным часам.</summary>
    public static int CountLessonsStartingBetweenForShift(
        IReadOnlyList<BellPeriod> allPeriods,
        int classShift,
        TimeSpan afterEnd,
        TimeSpan beforeStart)
    {
        if (beforeStart <= afterEnd)
            return 0;

        var seenStarts = new HashSet<TimeSpan>();
        foreach (var period in allPeriods)
        {
            if (!BellPeriodKinds.IsLesson(period.PeriodKind))
                continue;
            if (period.Shift != classShift)
                continue;
            if (!TryParseClock(period.StartTime, out var start))
                continue;
            if (start >= afterEnd && start < beforeStart)
                seenStarts.Add(start);
        }

        return seenStarts.Count;
    }

    /// <summary>Сколько уроков по звонкам начинается в интервале (afterEnd; beforeStart).</summary>
    public static int CountLessonsStartingBetween(
        IReadOnlyList<BellPeriod> allPeriods,
        int classGrade,
        int classShift,
        TimeSpan afterEnd,
        TimeSpan beforeStart)
    {
        if (beforeStart <= afterEnd)
            return 0;

        var count = 0;
        foreach (var period in allPeriods)
        {
            if (!BellPeriodKinds.IsLesson(period.PeriodKind))
                continue;
            if (period.Shift != classShift || !period.MatchesGrade(classGrade))
                continue;
            if (!TryParseClock(period.StartTime, out var start))
                continue;
            if (start >= afterEnd && start < beforeStart)
                count++;
        }

        return count;
    }

    public static bool TimesOverlap(LessonSlot a, LessonSlot b)
    {
        if (a.IsCancelled || b.IsCancelled)
            return false;

        if (TryParseTimeRange(a.StartTime, a.EndTime, out var aStart, out var aEnd) &&
            TryParseTimeRange(b.StartTime, b.EndTime, out var bStart, out var bEnd))
            return aStart < bEnd && bStart < aEnd;

        return a.LessonNumber == b.LessonNumber;
    }

    private static bool TryParseTimeRange(string start, string end, out TimeSpan startTs, out TimeSpan endTs)
    {
        startTs = default;
        endTs = default;
        if (string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end))
            return false;

        if (TimeSpan.TryParse(start, out startTs) && TimeSpan.TryParse(end, out endTs))
            return true;

        return TimeOnly.TryParse(start, out var so) &&
               TimeOnly.TryParse(end, out var eo) &&
               (startTs = so.ToTimeSpan()) <= (endTs = eo.ToTimeSpan());
    }
}
