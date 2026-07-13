using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>
/// Учебный ли день с учётом календаря (ТЗ §3): пн–пт, компенсационная суббота; каникулы и праздники — нет.
/// Вход: дата и исключения календаря. Выход: признак учебного дня и подсчёт дней.
/// </summary>
public static class SchoolDayCalendar
{
    public static bool IsSchoolDay(DateOnly date, IReadOnlyList<CalendarEntry> calendar)
    {
        var exception = FindCoveringException(date, calendar);
        if (exception is not null)
        {
            return exception.ExceptionType switch
            {
                CalendarExceptionTypes.Holiday or CalendarExceptionTypes.Vacation => false,
                CalendarExceptionTypes.Compensation => true,
                _ => IsRegularSchoolWeekday(date)
            };
        }

        return IsRegularSchoolWeekday(date);
    }

    public static int CountSchoolDaysAfter(
        DateOnly after,
        DateOnly toInclusive,
        IReadOnlyList<CalendarEntry> calendar)
    {
        var count = 0;
        for (var day = after.AddDays(1); day <= toInclusive; day = day.AddDays(1))
        {
            if (IsSchoolDay(day, calendar))
                count++;
        }

        return count;
    }

    public static DateOnly? FindLastSchoolDay(
        DateOnly fromInclusive,
        DateOnly toInclusive,
        IReadOnlyList<CalendarEntry> calendar)
    {
        for (var day = toInclusive; day >= fromInclusive; day = day.AddDays(-1))
        {
            if (IsSchoolDay(day, calendar))
                return day;
        }

        return null;
    }

    private static bool IsRegularSchoolWeekday(DateOnly date) =>
        date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;

    private static CalendarEntry? FindCoveringException(DateOnly date, IReadOnlyList<CalendarEntry> calendar)
    {
        CalendarEntry? match = null;
        foreach (var entry in calendar)
        {
            if (!TryParseRange(entry, out var start, out var end))
                continue;

            if (date < start || date > end)
                continue;

            match = entry;
        }

        return match;
    }

    private static bool TryParseRange(CalendarEntry entry, out DateOnly start, out DateOnly end)
    {
        start = default;
        end = default;
        if (!DateOnly.TryParse(entry.StartDate, out start))
            return false;

        end = DateOnly.TryParse(entry.EndDate, out var parsedEnd) ? parsedEnd : start;
        if (end < start)
            (start, end) = (end, start);

        return true;
    }
}
