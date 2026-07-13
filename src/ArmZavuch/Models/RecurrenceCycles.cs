namespace ArmZavuch.Models;

/// <summary>Цикл чередования шаблонов расписания.</summary>
public static class RecurrenceCycles
{
    public const string EveryWeek = "EveryWeek";
    public const string EveryOtherWeek = "EveryOtherWeek";

    public static string ToDisplay(string cycle) =>
        cycle == EveryOtherWeek ? "Через неделю (А/Б)" : "Каждую неделю";

    public static string Normalize(string? cycle) =>
        cycle == EveryOtherWeek ? EveryOtherWeek : EveryWeek;
}
