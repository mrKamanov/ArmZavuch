namespace ArmZavuch.Models;

/// <summary>Тип учебного периода (хранится в БД на англ., в UI — по-русски).</summary>
public static class PeriodTypes
{
    public const string Quarter = "Quarter";
    public const string Trimester = "Trimester";
    public const string HalfYear = "HalfYear";
    public const string Year = "Year";
    public const string Module = "Module";

    public static string ToDisplay(string type) => type switch
    {
        Trimester => "Триместр",
        HalfYear => "Полугодие",
        Year => "Учебный год",
        Module => "Модуль",
        _ => "Четверть"
    };

    public static bool RequiresGradeReminder(string type) => type is Quarter or Trimester or Year;

    /// <summary>Меньше — более «мелкий» период при одинаковой длине.</summary>
    public static int SpecificityRank(string type) => type switch
    {
        Module => 0,
        Quarter => 1,
        Trimester => 2,
        HalfYear => 3,
        Year => 4,
        _ => 5
    };
}
