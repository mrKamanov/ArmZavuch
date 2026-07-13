namespace ArmZavuch.Models;

/// <summary>Режимы сводной простыни расписания.</summary>
public static class OverviewViewModes
{
    public const string School = "School";
    public const string Teachers = "Teachers";
    public const string Classes = "Classes";
    public const string Buildings = "Buildings";

    public static readonly string[] All = [School, Teachers, Classes, Buildings];

    public static string ToDisplay(string mode) => mode switch
    {
        Teachers => "Педагоги",
        Classes => "Классы",
        Buildings => "Здания",
        _ => "Вся школа"
    };
}
