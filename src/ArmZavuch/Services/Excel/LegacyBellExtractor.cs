using ArmZavuch.Models;

namespace ArmZavuch.Services.Excel;

/// <summary>Разбор времени звонков из legacy-расписания (колонка «№ п/п»).</summary>
public static class LegacyBellTimeParser
{
    private static readonly System.Text.RegularExpressions.Regex TimeRangeRegex = new(
        @"(?<start>.+?)\s*-\s*(?<end>.+)",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    public static bool TryParseRange(string? raw, out string start, out string end)
    {
        start = "";
        end = "";
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        raw = raw.Trim();
        if (IsDynamicPauseLabel(raw))
            return false;

        var match = TimeRangeRegex.Match(raw);
        if (!match.Success)
            return false;

        return TryParseClock(match.Groups["start"].Value, out start)
               && TryParseClock(match.Groups["end"].Value, out end);
    }

    public static bool IsDynamicPauseLabel(string? raw) =>
        !string.IsNullOrWhiteSpace(raw)
        && raw.Trim().Contains("динам", StringComparison.OrdinalIgnoreCase)
        && raw.Trim().Contains("пауз", StringComparison.OrdinalIgnoreCase);

    public static bool LooksLikeTimeRange(string? raw) =>
        !string.IsNullOrWhiteSpace(raw)
        && raw.Contains('-')
        && raw.Any(char.IsDigit);

    public static bool TryParseClock(string? raw, out string hhmm)
    {
        hhmm = "";
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        raw = System.Text.RegularExpressions.Regex.Replace(raw.Trim(), @"\s+", "");
        if (raw.Contains('.'))
        {
            var parts = raw.Split('.', 2);
            if (!int.TryParse(parts[0], System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var hour))
                return false;
            if (!int.TryParse(parts[1], System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var minute))
                return false;

            hhmm = Format(hour, minute);
            return true;
        }

        if (!int.TryParse(raw, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var compact))
            return false;

        if (compact is >= 0 and <= 23)
        {
            hhmm = Format(compact, 0);
            return true;
        }

        if (raw.Length is 3 or 4)
        {
            hhmm = Format(compact / 100, compact % 100);
            return true;
        }

        return false;
    }

    public static bool TryParseMinutes(string hhmm, out int minutes)
    {
        minutes = 0;
        var parts = hhmm.Split(':');
        if (parts.Length != 2)
            return false;
        if (!int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m))
            return false;
        minutes = h * 60 + m;
        return true;
    }

    public static string Format(int hour, int minute) =>
        $"{hour:00}:{minute:00}";
}

public sealed record LegacyBellEntry(
    string PeriodKind,
    int LessonNumber,
    string StartTime,
    string EndTime);

public sealed record LegacyBellSchedule(
    int Shift,
    IReadOnlyList<LegacyBellEntry> Entries,
    string Signature)
{
    public string TemplateName
    {
        get
        {
            var lessons = Entries.Where(e => BellPeriodKinds.IsLesson(e.PeriodKind)).ToList();
            var first = lessons.FirstOrDefault()?.StartTime ?? "??:??";
            var last = lessons.LastOrDefault()?.EndTime ?? first;
            return BellTemplateNaming.FormatImportedShiftName(Shift, first, last);
        }
    }
}

/// <summary>Извлекает звонки из листа класса (достаточно блока понедельника).</summary>
public static class LegacyBellExtractor
{
    private static readonly HashSet<string> Weekdays = new(StringComparer.OrdinalIgnoreCase)
    {
        "ПОНЕДЕЛЬНИК", "ВТОРНИК", "СРЕДА", "ЧЕТВЕРГ", "ПЯТНИЦА", "СУББОТА"
    };

    public static LegacyBellSchedule? ExtractMondaySchedule(List<string?[]> rows, bool seniorLayout)
    {
        if (rows.Count < 3)
            return null;

        var subjectCol = seniorLayout ? 2 : 1;
        var mondayRows = CollectMondayRows(rows, subjectCol);
        if (mondayRows.Count == 0)
            mondayRows = CollectFirstDayRows(rows, subjectCol);

        var entries = BuildEntries(mondayRows);
        if (entries.Count == 0)
            return null;

        var shift = DetectShift(entries);
        return new LegacyBellSchedule(shift, entries, BuildSignature(shift, entries));
    }

    private static List<string> CollectMondayRows(List<string?[]> rows, int subjectCol)
    {
        var result = new List<string>();
        var inMonday = false;

        for (var r = 2; r < rows.Count; r++)
        {
            var day = Cell(rows[r], subjectCol);
            if (IsWeekday(day))
            {
                if (day.Equals("ПОНЕДЕЛЬНИК", StringComparison.OrdinalIgnoreCase))
                {
                    inMonday = true;
                    continue;
                }

                if (inMonday)
                    break;
            }

            if (!inMonday)
                continue;

            var timeCell = Cell(rows[r], 0);
            if (timeCell.Length > 0)
                result.Add(timeCell);
        }

        return result;
    }

    private static List<string> CollectFirstDayRows(List<string?[]> rows, int subjectCol)
    {
        var result = new List<string>();
        var inDay = false;

        for (var r = 2; r < rows.Count; r++)
        {
            var day = Cell(rows[r], subjectCol);
            if (IsWeekday(day))
            {
                if (inDay)
                    break;

                inDay = true;
                continue;
            }

            if (!inDay)
                continue;

            var timeCell = Cell(rows[r], 0);
            if (timeCell.Length > 0)
                result.Add(timeCell);
        }

        return result;
    }

    private static List<LegacyBellEntry> BuildEntries(IReadOnlyList<string> mondayRows)
    {
        var entries = new List<LegacyBellEntry>();
        string? lastLessonEnd = null;
        var lessonNumber = 0;

        for (var i = 0; i < mondayRows.Count; i++)
        {
            var cell = mondayRows[i];
            if (LegacyBellTimeParser.IsDynamicPauseLabel(cell))
            {
                if (lessonNumber == 0 || lastLessonEnd is null)
                    continue;

                var pauseStart = lastLessonEnd;
                var pauseEnd = FindNextLessonStart(mondayRows, i + 1) ?? AddMinutes(pauseStart, 20);
                entries.Add(new LegacyBellEntry(BellPeriodKinds.DynamicPause, lessonNumber, pauseStart, pauseEnd));
                continue;
            }

            if (!LegacyBellTimeParser.TryParseRange(cell, out var lessonStart, out var lessonEnd))
                continue;

            if (lastLessonEnd is not null
                && LegacyBellTimeParser.TryParseMinutes(lastLessonEnd, out var prevEnd)
                && LegacyBellTimeParser.TryParseMinutes(lessonStart, out var nextStart)
                && nextStart > prevEnd)
            {
                entries.Add(new LegacyBellEntry(BellPeriodKinds.Break, lessonNumber, lastLessonEnd, lessonStart));
            }

            lessonNumber++;
            entries.Add(new LegacyBellEntry(BellPeriodKinds.Lesson, lessonNumber, lessonStart, lessonEnd));
            lastLessonEnd = lessonEnd;
        }

        return entries;
    }

    private static string? FindNextLessonStart(IReadOnlyList<string> rows, int startIndex)
    {
        for (var i = startIndex; i < rows.Count; i++)
        {
            if (LegacyBellTimeParser.IsDynamicPauseLabel(rows[i]))
                continue;

            if (LegacyBellTimeParser.TryParseRange(rows[i], out var start, out _))
                return start;
        }

        return null;
    }

    private static string AddMinutes(string hhmm, int minutes)
    {
        if (!LegacyBellTimeParser.TryParseMinutes(hhmm, out var total))
            return hhmm;

        total += minutes;
        return LegacyBellTimeParser.Format(total / 60, total % 60);
    }

    private static int DetectShift(IReadOnlyList<LegacyBellEntry> entries)
    {
        var firstLesson = entries.FirstOrDefault(e => BellPeriodKinds.IsLesson(e.PeriodKind));
        if (firstLesson is null)
            return 1;

        return LegacyBellTimeParser.TryParseMinutes(firstLesson.StartTime, out var minutes) && minutes >= 11 * 60
            ? 2
            : 1;
    }

    private static string BuildSignature(int shift, IReadOnlyList<LegacyBellEntry> entries) =>
        shift + "|" + string.Join(";", entries.Select(e =>
            $"{e.PeriodKind}:{e.LessonNumber}:{e.StartTime}-{e.EndTime}"));

    private static bool IsWeekday(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Weekdays.Contains(value.Trim());

    private static string Cell(string?[] row, int index) => row.ElementAtOrDefault(index)?.Trim() ?? "";
}
