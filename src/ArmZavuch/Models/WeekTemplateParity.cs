namespace ArmZavuch.Models;

/// <summary>Метка недельного шаблона (А/Б) для чередующейся нагрузки.</summary>
public static class WeekTemplateParity
{
    public const string Any = "Any";
    public const string WeekA = "WeekA";
    public const string WeekB = "WeekB";

    public static readonly string[] All = [Any, WeekA, WeekB];

    public static string ToDisplay(string parity) => parity switch
    {
        WeekA => "Неделя А",
        WeekB => "Неделя Б",
        _ => "Обычная (каждую неделю)"
    };

    public static string InferFromName(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("неделя б") || n.Contains("неделя b") || n.EndsWith(" б") || n.EndsWith(" b"))
            return WeekB;
        if (n.Contains("неделя а") || n.Contains("неделя a") || n.EndsWith(" а") || n.EndsWith(" a"))
            return WeekA;
        return Any;
    }
}
