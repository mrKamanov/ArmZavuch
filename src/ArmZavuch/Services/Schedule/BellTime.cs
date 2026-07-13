using System.Globalization;

namespace ArmZavuch.Services.Schedule;

/// <summary>
/// Разбор времени звонков (ЧЧ:ММ) и длительности интервалов.
/// Вход: строки времени. Выход: минуты, сравнение, форматирование.
/// </summary>
public static class BellTime
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static bool TryParse(string? value, out TimeOnly time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        if (TimeOnly.TryParse(trimmed, Invariant, DateTimeStyles.None, out time))
            return true;

        return TimeOnly.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.None, out time);
    }

    public static int? TryDurationMinutes(string? start, string? end)
    {
        if (!TryParse(start, out var from) || !TryParse(end, out var to))
            return null;

        var minutes = (int)(to.ToTimeSpan() - from.ToTimeSpan()).TotalMinutes;
        return minutes > 0 ? minutes : null;
    }

    public static string FormatDuration(string? start, string? end)
    {
        var minutes = TryDurationMinutes(start, end);
        return minutes is null ? "" : $"{minutes} мин";
    }

    public static string FormatRange(string? start, string? end)
    {
        if (string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end))
            return "";
        return $"{start.Trim()}–{end.Trim()}";
    }

    public static int CompareStart(string? left, string? right)
    {
        if (!TryParse(left, out var l))
            return 1;
        if (!TryParse(right, out var r))
            return -1;
        return l.CompareTo(r);
    }

    public static string AddMinutes(string? start, int minutes)
    {
        if (!TryParse(start, out var time))
            return start ?? "";

        return time.AddMinutes(minutes).ToString("HH:mm", Invariant);
    }

    public static string ShiftMinutes(string? start, int minutes) => AddMinutes(start, minutes);

    public static bool TryAutoFormat(string? value, out string formatted)
    {
        formatted = value ?? "";
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        if (trimmed.Contains(':'))
        {
            formatted = NormalizeColonInput(trimmed);
            return !formatted.Equals(trimmed, StringComparison.Ordinal);
        }

        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.Length == 4)
        {
            formatted = $"{digits[..2]}:{digits[2..]}";
            return true;
        }

        if (digits.Length == 3)
        {
            formatted = $"0{digits[0]}:{digits[1..]}";
            return true;
        }

        return false;
    }

    public static string NormalizeInput(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        if (TryAutoFormat(value, out var formatted))
            return formatted;

        return value.Trim().Contains(':')
            ? NormalizeColonInput(value.Trim())
            : value.Trim();
    }

    public static bool IntervalsOverlap(string aStart, string aEnd, string bStart, string bEnd)
    {
        if (!TryParse(aStart, out var as_) || !TryParse(aEnd, out var ae)
            || !TryParse(bStart, out var bs) || !TryParse(bEnd, out var be))
            return false;

        return as_ < be && bs < ae;
    }

    private static string NormalizeColonInput(string trimmed)
    {
        var parts = trimmed.Split(':', 2);
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var hours)
            || !int.TryParse(parts[1], out var minutes))
            return trimmed;

        hours = Math.Clamp(hours, 0, 23);
        minutes = Math.Clamp(minutes, 0, 59);
        return $"{hours:D2}:{minutes:D2}";
    }
}
