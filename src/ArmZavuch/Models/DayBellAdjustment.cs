namespace ArmZavuch.Models;

/// <summary>
/// Суточная корректировка звонков без смены недельного шаблона.
/// Хранится в note записи day_overrides типа ShortenedDay
/// (adj:lessons:5;breaks:3;lessonlen:30;breaklen:10;maxlessons:6;nopause:1).
/// </summary>
public sealed class DayBellAdjustment
{
    public int? ShortLessonsMinutes { get; init; }
    public int? ShortBreaksMinutes { get; init; }
    public int? FixedLessonsMinutes { get; init; }
    public int? FixedBreaksMinutes { get; init; }
    public int? MaxLessons { get; init; }
    public bool SkipDynamicPause { get; init; }

    public bool IsEmpty =>
        ShortLessonsMinutes is null
        && ShortBreaksMinutes is null
        && FixedLessonsMinutes is null
        && FixedBreaksMinutes is null
        && MaxLessons is null
        && !SkipDynamicPause;

    public string StorageNote
    {
        get
        {
            var parts = new List<string>();
            if (ShortLessonsMinutes is int lessonMinutes)
                parts.Add($"lessons:{lessonMinutes}");
            if (ShortBreaksMinutes is int breakMinutes)
                parts.Add($"breaks:{breakMinutes}");
            if (FixedLessonsMinutes is int fixedLessons)
                parts.Add($"lessonlen:{fixedLessons}");
            if (FixedBreaksMinutes is int fixedBreaks)
                parts.Add($"breaklen:{fixedBreaks}");
            if (MaxLessons is int maxLessons)
                parts.Add($"maxlessons:{maxLessons}");
            if (SkipDynamicPause)
                parts.Add("nopause:1");
            return parts.Count == 0 ? "" : $"{Prefix}{string.Join(';', parts)}";
        }
    }

    public string DisplayLine
    {
        get
        {
            var parts = new List<string>();
            if (ShortLessonsMinutes is int lessonMinutes)
                parts.Add($"уроки −{lessonMinutes} мин");
            if (ShortBreaksMinutes is int breakMinutes)
                parts.Add($"перемены −{breakMinutes} мин");
            if (FixedLessonsMinutes is int fixedLessons)
                parts.Add($"уроки по {fixedLessons} мин");
            if (FixedBreaksMinutes is int fixedBreaks)
                parts.Add($"перемены по {fixedBreaks} мин");
            if (MaxLessons is int maxLessons)
                parts.Add($"не более {maxLessons} уроков");
            if (SkipDynamicPause)
                parts.Add("без дин. паузы");
            return parts.Count == 0 ? "" : string.Join(" · ", parts);
        }
    }

    private const string Prefix = "adj:";

    public DayBellAdjustment WithShortLessons(int minutes) => Copy(shortLessonsMinutes: minutes);

    public DayBellAdjustment WithShortBreaks(int minutes) => Copy(shortBreaksMinutes: minutes);

    public DayBellAdjustment WithFixedLessons(int minutes) => Copy(fixedLessonsMinutes: minutes);

    public DayBellAdjustment WithFixedBreaks(int minutes) => Copy(fixedBreaksMinutes: minutes);

    public DayBellAdjustment WithMaxLessons(int maxLessons) => Copy(maxLessons: maxLessons);

    public DayBellAdjustment WithSkipDynamicPause(bool skip = true) => Copy(skipDynamicPause: skip);

    private DayBellAdjustment Copy(
        int? shortLessonsMinutes = null,
        int? shortBreaksMinutes = null,
        int? fixedLessonsMinutes = null,
        int? fixedBreaksMinutes = null,
        int? maxLessons = null,
        bool? skipDynamicPause = null) => new()
    {
        ShortLessonsMinutes = shortLessonsMinutes ?? ShortLessonsMinutes,
        ShortBreaksMinutes = shortBreaksMinutes ?? ShortBreaksMinutes,
        FixedLessonsMinutes = fixedLessonsMinutes ?? FixedLessonsMinutes,
        FixedBreaksMinutes = fixedBreaksMinutes ?? FixedBreaksMinutes,
        MaxLessons = maxLessons ?? MaxLessons,
        SkipDynamicPause = skipDynamicPause ?? SkipDynamicPause
    };

    public static DayBellAdjustment? TryParse(string? note)
    {
        if (string.IsNullOrWhiteSpace(note) || !note.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        int? lessonMinutes = null;
        int? breakMinutes = null;
        int? fixedLessons = null;
        int? fixedBreaks = null;
        int? maxLessons = null;
        var skipDynamicPause = false;

        foreach (var token in note[Prefix.Length..].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = token.Split(':', 2);
            if (parts.Length != 2)
                continue;

            if (parts[0].Equals("nopause", StringComparison.OrdinalIgnoreCase)
                && parts[1] == "1")
            {
                skipDynamicPause = true;
                continue;
            }

            if (!int.TryParse(parts[1], out var value) || value < 1)
                continue;

            switch (parts[0].ToLowerInvariant())
            {
                case "lessons":
                    lessonMinutes = value;
                    break;
                case "breaks":
                    breakMinutes = value;
                    break;
                case "lessonlen":
                    fixedLessons = value;
                    break;
                case "breaklen":
                    fixedBreaks = value;
                    break;
                case "maxlessons":
                    maxLessons = value;
                    break;
            }
        }

        if (lessonMinutes is null && breakMinutes is null && fixedLessons is null && fixedBreaks is null
            && maxLessons is null && !skipDynamicPause)
            return null;

        return new DayBellAdjustment
        {
            ShortLessonsMinutes = lessonMinutes,
            ShortBreaksMinutes = breakMinutes,
            FixedLessonsMinutes = fixedLessons,
            FixedBreaksMinutes = fixedBreaks,
            MaxLessons = maxLessons,
            SkipDynamicPause = skipDynamicPause
        };
    }
}
