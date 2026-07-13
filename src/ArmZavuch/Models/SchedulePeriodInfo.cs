using System.Globalization;

namespace ArmZavuch.Models;

/// <summary>Период действия расписания (ТЗ §3).</summary>
public sealed class SchedulePeriodInfo : SelectableEntity
{
    private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string PeriodType { get; set; } = "Quarter";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public string RecurrenceCycle { get; set; } = RecurrenceCycles.EveryWeek;

    public string PeriodTypeDisplay => PeriodTypes.ToDisplay(PeriodType);

    public string RecurrenceCycleDisplay => RecurrenceCycles.ToDisplay(RecurrenceCycle);

    public string StartDateDisplay => FormatDisplayDate(StartDate);

    public string EndDateDisplay => FormatDisplayDate(EndDate);

    public string DateRangeDisplay => $"{StartDateDisplay} — {EndDateDisplay}";

    public string RowToolTip =>
        $"{Name} ({PeriodTypeDisplay})\n{DateRangeDisplay}\nЧередование: {RecurrenceCycleDisplay}";

    private static string FormatDisplayDate(string? value) =>
        DateOnly.TryParse(value, out var date)
            ? date.ToString("dd.MM.yyyy", RussianCulture)
            : value ?? "";
}
