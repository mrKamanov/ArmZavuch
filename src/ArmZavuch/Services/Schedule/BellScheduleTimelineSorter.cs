using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>Хронологическая сортировка строк шаблона звонков для отображения в UI.</summary>
public static class BellScheduleTimelineSorter
{
    public static IEnumerable<BellPeriod> OrderForDisplay(IEnumerable<BellPeriod> periods) =>
        periods
            .OrderBy(p => p.Shift)
            .ThenBy(p => BellTime.CompareStart(p.StartTime, null))
            .ThenBy(p => KindRank(p.PeriodKind))
            .ThenBy(p => p.LessonNumber);

    private static int KindRank(string kind) => kind switch
    {
        BellPeriodKinds.Lesson => 0,
        BellPeriodKinds.DynamicPause => 1,
        BellPeriodKinds.Break => 2,
        _ => 3
    };
}
