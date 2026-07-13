using ArmZavuch.Models;

namespace ArmZavuch.Services.Export;

/// <summary>Сводка отсутствий и замен за период: строки журнала, агрегаты, данные диаграмм.</summary>
public static class StaffActivityReportBuilder
{
    private const double MaxBarPixels = 280;

    public static List<AbsenceHistoryRow> BuildAbsenceRows(
        IReadOnlyList<TeacherStatusPeriod> periods,
        IReadOnlyDictionary<int, Teacher> teachers,
        DateOnly from,
        DateOnly to) =>
        periods
            .Where(p => teachers.ContainsKey(p.TeacherId))
            .Select(p =>
            {
                var start = Max(DateOnly.Parse(p.StartDate), from);
                var end = Min(ResolveEnd(p, to), to);
                var days = end < start ? 0 : end.DayNumber - start.DayNumber + 1;
                return new AbsenceHistoryRow
                {
                    PeriodId = p.Id,
                    TeacherId = p.TeacherId,
                    TeacherName = teachers[p.TeacherId].FullName,
                    StatusType = p.StatusType,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    DaysInRange = days,
                    IsOfficial = p.IsOfficial,
                    Source = p.Source,
                    Note = p.Note
                };
            })
            .OrderBy(r => r.TeacherName, StringComparer.CurrentCultureIgnoreCase)
            .ThenByDescending(r => r.StartDate)
            .ToList();

    public static List<StaffActivitySummaryRow> BuildSummary(
        IReadOnlyList<TeacherStatusPeriod> periods,
        IReadOnlyList<Teacher> teachers,
        IReadOnlyList<SubstitutionRecord> substitutions,
        DateOnly from,
        DateOnly to)
    {
        var byTeacher = periods.GroupBy(p => p.TeacherId).ToDictionary(g => g.Key, g => g.ToList());
        var rows = new List<StaffActivitySummaryRow>();

        foreach (var teacher in teachers.OrderBy(t => t.FullName, StringComparer.CurrentCultureIgnoreCase))
        {
            byTeacher.TryGetValue(teacher.Id, out var teacherPeriods);
            teacherPeriods ??= [];

            var sick = 0;
            var leave = 0;
            var other = 0;
            var absenceDays = 0;

            for (var day = from; day <= to; day = day.AddDays(1))
            {
                var active = teacherPeriods.FirstOrDefault(p => IsActiveOn(p, day));
                if (active is null)
                    continue;

                absenceDays++;
                switch (active.StatusType)
                {
                    case StaffStatusTypes.Sick: sick++; break;
                    case StaffStatusTypes.Leave: leave++; break;
                    default: other++; break;
                }
            }

            var teacherSubs = substitutions.Where(s => s.ReplacementTeacherId == teacher.Id).ToList();
            var substitutionCount = teacherSubs.Count;
            var officialSubs = teacherSubs.Count(s => s.IsOfficial);
            var wasReplaced = substitutions.Count(s => s.AbsentTeacherId == teacher.Id);
            if (absenceDays == 0 && substitutionCount == 0 && wasReplaced == 0)
                continue;

            rows.Add(new StaffActivitySummaryRow
            {
                TeacherId = teacher.Id,
                TeacherName = teacher.FullName,
                AbsenceDays = absenceDays,
                SickDays = sick,
                LeaveDays = leave,
                OtherDays = other,
                SubstitutionCount = substitutionCount,
                OfficialSubstitutionCount = officialSubs,
                UnofficialSubstitutionCount = substitutionCount - officialSubs,
                WasReplacedCount = wasReplaced
            });
        }

        return rows
            .OrderByDescending(r => r.AbsenceDays)
            .ThenByDescending(r => r.SubstitutionCount)
            .ThenBy(r => r.TeacherName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static List<StaffBarChartPoint> BuildAbsenteeChart(
        IReadOnlyList<StaffActivitySummaryRow> summary,
        int maxItems = 8) =>
        BuildChart(summary.Where(r => r.AbsenceDays > 0), r => r.AbsenceDays, r => $"{r.AbsenceDays} дн.", maxItems);

    public static List<StaffBarChartPoint> BuildSubstitutorChart(
        IReadOnlyList<StaffActivitySummaryRow> summary,
        int maxItems = 8) =>
        BuildChart(summary.Where(r => r.SubstitutionCount > 0), r => r.SubstitutionCount, r => $"{r.SubstitutionCount} зам.", maxItems);

    private static List<StaffBarChartPoint> BuildChart(
        IEnumerable<StaffActivitySummaryRow> source,
        Func<StaffActivitySummaryRow, int> valueSelector,
        Func<StaffActivitySummaryRow, string> captionSelector,
        int maxItems)
    {
        var items = source
            .OrderByDescending(valueSelector)
            .ThenBy(r => r.TeacherName, StringComparer.CurrentCultureIgnoreCase)
            .Take(maxItems)
            .ToList();
        if (items.Count == 0)
            return [];

        var max = Math.Max(1, items.Max(valueSelector));
        return items
            .Select(r => new StaffBarChartPoint
            {
                Label = r.TeacherName,
                Caption = captionSelector(r),
                BarPixelWidth = valueSelector(r) / (double)max * MaxBarPixels
            })
            .ToList();
    }

    private static bool IsActiveOn(TeacherStatusPeriod period, DateOnly day)
    {
        var start = DateOnly.Parse(period.StartDate);
        if (day < start)
            return false;
        if (period.IsOpen)
            return true;
        return day <= DateOnly.Parse(period.EndDate!);
    }

    private static DateOnly ResolveEnd(TeacherStatusPeriod period, DateOnly reportTo) =>
        period.IsOpen ? reportTo : DateOnly.Parse(period.EndDate!);

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
}
