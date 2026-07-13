using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>
/// Напоминание за 2 учебных дня до конца четверти, триместра или учебного года (ТЗ §3).
/// Вход: дата и календарь. Выход: подпись для диспетчерской или null.
/// </summary>
public sealed class PeriodGradeReminderService
{
    private const int ReminderSchoolDaysBeforeEnd = 2;

    private readonly SchedulePeriodRepository _periods;
    private readonly CalendarRepository _calendar;

    public PeriodGradeReminderService(SchedulePeriodRepository periods, CalendarRepository calendar)
    {
        _periods = periods;
        _calendar = calendar;
    }

    public async Task<PeriodGradeReminder?> ResolveAsync(DateOnly date)
    {
        var entries = await _periods.GetAllAsync();
        var calendar = await _calendar.GetAllAsync();
        return Resolve(date, entries, calendar);
    }

    internal static PeriodGradeReminder? Resolve(
        DateOnly date,
        IReadOnlyList<SchedulePeriodInfo> entries,
        IReadOnlyList<CalendarEntry> calendar)
    {
        if (!SchoolDayCalendar.IsSchoolDay(date, calendar))
            return null;

        PeriodGradeReminder? match = null;
        var nearestEnd = int.MaxValue;

        foreach (var entry in entries)
        {
            if (!PeriodTypes.RequiresGradeReminder(entry.PeriodType))
                continue;

            if (!TryParseDate(entry.StartDate, out var start) || !TryParseDate(entry.EndDate, out var end))
                continue;

            if (date < start || date > end)
                continue;

            var effectiveEnd = SchoolDayCalendar.FindLastSchoolDay(start, end, calendar);
            if (effectiveEnd is not DateOnly lastSchoolDay || date > lastSchoolDay)
                continue;

            var schoolDaysUntilEnd = SchoolDayCalendar.CountSchoolDaysAfter(date, lastSchoolDay, calendar);
            if (schoolDaysUntilEnd != ReminderSchoolDaysBeforeEnd)
                continue;

            if (lastSchoolDay.DayNumber >= nearestEnd)
                continue;

            nearestEnd = lastSchoolDay.DayNumber;
            match = new PeriodGradeReminder
            {
                PeriodName = entry.Name,
                SchoolDaysUntilEnd = schoolDaysUntilEnd
            };
        }

        return match;
    }

    private static bool TryParseDate(string? value, out DateOnly date) =>
        DateOnly.TryParse(value, out date);
}
