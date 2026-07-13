using System.Globalization;

namespace ArmZavuch.Models;

/// <summary>
/// Обратный отсчёт до ближайшего события учебного календаря (ТЗ §3).
/// Вход: дата и запись календаря. Выход: подписи для компактного чипа в диспетчерской.
/// </summary>
public sealed class CalendarCountdown
{
    private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

    public string TypeLabel { get; init; } = "";
    public string? Note { get; init; }
    public bool IsOngoing { get; init; }
    public int DaysUntilStart { get; init; }
    public int DaysRemaining { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }

    public string ChipLabel => IsOngoing
        ? ShortTypeLabel
        : DaysUntilStart <= 1
            ? ShortTypeLabel
            : $"До {ShortTypeLabelGenitive}";

    public string ChipValue => IsOngoing
        ? FormatOngoingValue()
        : DaysUntilStart switch
        {
            0 => "сегодня",
            1 => "завтра",
            _ => DaysUntilStart.ToString()
        };

    public string ChipHint => IsOngoing
        ? FormatOngoingHint()
        : FormatUpcomingHint();

    public string AccentBackground => ExceptionType switch
    {
        CalendarExceptionTypes.Vacation => IsOngoing ? "#E0F2FE" : "#F0F9FF",
        CalendarExceptionTypes.Compensation => "#F5F3FF",
        _ => "#FFFBEB"
    };

    public string AccentForeground => ExceptionType switch
    {
        CalendarExceptionTypes.Vacation => IsOngoing ? "#0369A1" : "#0284C7",
        CalendarExceptionTypes.Compensation => "#7C3AED",
        _ => "#B45309"
    };

    internal string ExceptionType { get; init; } = CalendarExceptionTypes.Holiday;

    private string ShortTypeLabel => ExceptionType switch
    {
        CalendarExceptionTypes.Vacation => "Каникулы",
        CalendarExceptionTypes.Compensation => "Компенсация",
        _ => "Праздник"
    };

    private string ShortTypeLabelGenitive => ExceptionType switch
    {
        CalendarExceptionTypes.Vacation => "каникул",
        CalendarExceptionTypes.Compensation => "субботы",
        _ => "праздника"
    };

    private string FormatOngoingValue()
    {
        if (DaysRemaining == 1)
            return StartDate == EndDate ? "сегодня" : "последний";

        return $"ещё {DaysRemaining}";
    }

    private string FormatOngoingHint()
    {
        if (DaysRemaining > 1)
            return FormatDaysWord(DaysRemaining);

        if (DaysRemaining == 1 && StartDate != EndDate)
            return "день";

        var note = TrimNote();
        if (note is not null)
            return note;

        return StartDate == EndDate
            ? FormatRussianDate(StartDate)
            : $"до {FormatRussianDate(EndDate)}";
    }

    private string FormatUpcomingHint()
    {
        if (DaysUntilStart <= 1)
            return FormatSoonHint();

        var daysWord = FormatDaysWord(DaysUntilStart);
        var note = TrimNote();
        if (note is not null)
            return $"{daysWord} · {note}";

        if (StartDate != EndDate)
            return $"{daysWord} · с {FormatRussianDate(StartDate)}";

        return daysWord;
    }

    private string FormatSoonHint()
    {
        var note = TrimNote();
        if (note is not null)
            return note;

        if (StartDate != EndDate)
            return $"с {FormatRussianDate(StartDate)}";

        return FormatRussianDate(StartDate);
    }

    private string? TrimNote()
    {
        if (string.IsNullOrWhiteSpace(Note))
            return null;

        var text = Note.Trim();
        return text.Length <= 24 ? text : text[..21] + "…";
    }

    private static string FormatRussianDate(DateOnly date) =>
        date.ToString("d MMMM", RussianCulture);

    private static string FormatDaysWord(int days) => days switch
    {
        1 => "день",
        >= 2 and <= 4 => "дня",
        _ => "дней"
    };
}
