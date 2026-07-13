using System.Globalization;
using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>
/// Выбор учебного периода и чередования шаблонов на дату (ТЗ §3).
/// Вход: дата и список периодов. Выход: самый короткий период и нужная неделя А/Б.
/// </summary>
public static class SchedulePeriodResolver
{
    private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

    public static SchedulePeriodInfo? ResolveMatchingPeriod(
        DateOnly date,
        IReadOnlyList<SchedulePeriodInfo> entries)
    {
        SchedulePeriodInfo? best = null;
        var bestSpan = int.MaxValue;
        var bestRank = int.MaxValue;

        foreach (var entry in entries)
        {
            if (!TryParseRange(entry, out var start, out var end))
                continue;

            if (date < start || date > end)
                continue;

            var span = end.DayNumber - start.DayNumber;
            var rank = PeriodTypes.SpecificityRank(entry.PeriodType);
            if (span > bestSpan || span == bestSpan && rank >= bestRank)
                continue;

            bestSpan = span;
            bestRank = rank;
            best = entry;
        }

        return best;
    }

    public static string ResolveTemplateParity(SchedulePeriodInfo period, DateOnly date)
    {
        if (!TryParseDate(period.StartDate, out var start))
            return WeekTemplateParity.Any;

        return period.RecurrenceCycle switch
        {
            RecurrenceCycles.EveryOtherWeek =>
                (date.DayNumber - start.DayNumber) / 7 % 2 == 0
                    ? WeekTemplateParity.WeekA
                    : WeekTemplateParity.WeekB,
            _ => WeekTemplateParity.Any
        };
    }

    public static string DescribeActiveRule(SchedulePeriodInfo period, DateOnly date)
    {
        var parity = ResolveTemplateParity(period, date);
        var cycle = RecurrenceCycles.ToDisplay(period.RecurrenceCycle);
        var weekLabel = parity switch
        {
            WeekTemplateParity.WeekA => " · неделя А",
            WeekTemplateParity.WeekB => " · неделя Б",
            _ => ""
        };

        return $"{period.Name}: {cycle}{weekLabel}";
    }

    public static int ResolveTemplateId(string parity, IReadOnlyList<WeekTemplateInfo> templates)
    {
        var exact = templates.FirstOrDefault(t => t.WeekParity == parity);
        if (exact is not null)
            return exact.Id;

        if (parity is WeekTemplateParity.WeekA or WeekTemplateParity.WeekB)
        {
            var inferred = templates.FirstOrDefault(t =>
                WeekTemplateParity.InferFromName(t.Name) == parity);
            if (inferred is not null)
                return inferred.Id;
        }

        var any = templates.FirstOrDefault(t => t.WeekParity == WeekTemplateParity.Any);
        if (any is not null)
            return any.Id;

        return templates.Count > 0 ? templates[0].Id : 0;
    }

    internal static bool TryParseRange(SchedulePeriodInfo entry, out DateOnly start, out DateOnly end)
    {
        start = default;
        end = default;
        if (!TryParseDate(entry.StartDate, out start) || !TryParseDate(entry.EndDate, out end))
            return false;

        if (end < start)
            (start, end) = (end, start);

        return true;
    }

    internal static bool TryParseDate(string? value, out DateOnly date) =>
        DateOnly.TryParse(value, out date);

    public static string FormatDisplayDate(string? value) =>
        DateOnly.TryParse(value, out var date)
            ? date.ToString("dd.MM.yyyy", RussianCulture)
            : value ?? "";
}
