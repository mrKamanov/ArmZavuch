using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>
/// Строит перемены между уроками шаблона по фактическим интервалам времени.
/// Вход: хронология шаблона. Выход: список недостающих перемен.
/// </summary>
public static class BellBreakGenerator
{
    public sealed record ProposedBreak(int AfterLessonNumber, string StartTime, string EndTime);

    public static List<ProposedBreak> GenerateMissingBreaks(IReadOnlyList<BellPeriod> timeline)
    {
        if (timeline.Count == 0)
            return [];

        var ordered = BellScheduleTimelineSorter.OrderForDisplay(timeline).ToList();
        var lessons = ordered
            .Where(p => BellPeriodKinds.IsLesson(p.PeriodKind))
            .OrderBy(p => BellTime.CompareStart(p.StartTime, null))
            .ThenBy(p => p.LessonNumber)
            .ToList();

        if (lessons.Count < 2)
            return [];

        var result = new List<ProposedBreak>();
        for (var i = 0; i < lessons.Count - 1; i++)
        {
            var current = lessons[i];
            var next = lessons[i + 1];
            var gapStart = current.EndTime.Trim();
            var gapEnd = next.StartTime.Trim();

            if (BellTime.CompareStart(gapStart, gapEnd) >= 0)
                continue;

            if (GapAlreadyCovered(ordered, gapStart, gapEnd))
                continue;

            if (ordered.Any(p =>
                    p.PeriodKind == BellPeriodKinds.Break
                    && p.LessonNumber == current.LessonNumber
                    && p.StartTime == gapStart
                    && p.EndTime == gapEnd))
                continue;

            result.Add(new ProposedBreak(current.LessonNumber, gapStart, gapEnd));
        }

        return result;
    }

    private static bool GapAlreadyCovered(
        IReadOnlyList<BellPeriod> ordered,
        string gapStart,
        string gapEnd)
    {
        foreach (var period in ordered)
        {
            if (BellPeriodKinds.IsLesson(period.PeriodKind))
                continue;

            if (BellTime.IntervalsOverlap(gapStart, gapEnd, period.StartTime, period.EndTime))
                return true;
        }

        return false;
    }
}
