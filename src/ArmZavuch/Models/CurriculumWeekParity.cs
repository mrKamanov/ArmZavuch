namespace ArmZavuch.Models;

/// <summary>Цикл нагрузки: каждую неделю или только неделя А/Б.</summary>
public static class CurriculumWeekParity
{
    public const string EveryWeek = "EveryWeek";
    public const string WeekA = "WeekA";
    public const string WeekB = "WeekB";

    public static readonly string[] All = [EveryWeek, WeekA, WeekB];

    public static string ToDisplay(string parity) => parity switch
    {
        WeekA => "Неделя А",
        WeekB => "Неделя Б",
        _ => "Каждую неделю"
    };

    public static string FromDisplay(string display)
    {
        if (display.Contains('Б', StringComparison.OrdinalIgnoreCase) && display.Contains("недел", StringComparison.OrdinalIgnoreCase))
            return WeekB;
        if (display.Contains('А', StringComparison.OrdinalIgnoreCase) && display.Contains("недел", StringComparison.OrdinalIgnoreCase))
            return WeekA;
        if (display.Equals("А", StringComparison.OrdinalIgnoreCase) || display.Equals("A", StringComparison.OrdinalIgnoreCase))
            return WeekA;
        if (display.Equals("Б", StringComparison.OrdinalIgnoreCase) || display.Equals("B", StringComparison.OrdinalIgnoreCase))
            return WeekB;
        return EveryWeek;
    }

    /// <summary>Показывать в палитре шаблона с заданной чётностью.</summary>
    public static bool MatchesForTemplate(string itemParity, string templateParity)
    {
        if (itemParity == EveryWeek)
            return true;
        if (templateParity == WeekTemplateParity.Any)
            return false;
        return itemParity == templateParity;
    }
}
