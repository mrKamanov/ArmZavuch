using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>
/// Ближайшее событие учебного календаря от заданной даты (ТЗ §3, календарь конструктора).
/// Вход: дата. Выход: обратный отсчёт или текущий период каникул/праздника.
/// </summary>
public sealed class CalendarCountdownService
{
    private readonly CalendarRepository _calendar;

    public CalendarCountdownService(CalendarRepository calendar) => _calendar = calendar;

    public async Task<CalendarCountdown?> ResolveNearestAsync(DateOnly fromDate)
    {
        var entries = await _calendar.GetAllAsync();
        return ResolveNearest(fromDate, entries);
    }

    internal static CalendarCountdown? ResolveNearest(DateOnly fromDate, IReadOnlyList<CalendarEntry> entries)
    {
        CalendarCountdown? nearestFuture = null;
        var nearestDays = int.MaxValue;
        var nearestPriority = int.MaxValue;

        foreach (var entry in entries)
        {
            if (!TryParseDate(entry.StartDate, out var start))
                continue;

            var end = TryParseDate(entry.EndDate, out var parsedEnd) ? parsedEnd : start;
            if (end < start)
                (start, end) = (end, start);

            if (fromDate >= start && fromDate <= end)
                return Create(entry, start, end, isOngoing: true, fromDate);

            if (start <= fromDate)
                continue;

            var daysUntil = start.DayNumber - fromDate.DayNumber;
            var priority = TypePriority(entry.ExceptionType);
            if (daysUntil > nearestDays || daysUntil == nearestDays && priority >= nearestPriority)
                continue;

            nearestDays = daysUntil;
            nearestPriority = priority;
            nearestFuture = Create(entry, start, end, isOngoing: false, fromDate);
        }

        return nearestFuture;
    }

    private static CalendarCountdown Create(
        CalendarEntry entry,
        DateOnly start,
        DateOnly end,
        bool isOngoing,
        DateOnly fromDate) =>
        new()
        {
            ExceptionType = entry.ExceptionType,
            TypeLabel = entry.TypeDisplay,
            Note = entry.Note,
            StartDate = start,
            EndDate = end,
            IsOngoing = isOngoing,
            DaysUntilStart = isOngoing ? 0 : start.DayNumber - fromDate.DayNumber,
            DaysRemaining = isOngoing ? end.DayNumber - fromDate.DayNumber + 1 : 0
        };

    private static int TypePriority(string type) => type switch
    {
        CalendarExceptionTypes.Vacation => 0,
        CalendarExceptionTypes.Holiday => 1,
        _ => 2
    };

    private static bool TryParseDate(string? value, out DateOnly date) =>
        DateOnly.TryParse(value, out date);
}
