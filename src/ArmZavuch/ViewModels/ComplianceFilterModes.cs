namespace ArmZavuch.ViewModels;

/// <summary>Режимы сортировки замечаний проверки норм.</summary>
public static class ComplianceIssueSortModes
{
    public const string Severity = "Severity";
    public const string ClassDay = "ClassDay";
    public const string Code = "Code";

    public static IReadOnlyList<string> All { get; } = [Severity, ClassDay, Code];

    public static string ToDisplay(string mode) => mode switch
    {
        ClassDay => "Класс и день",
        Code => "Тип замечания",
        _ => "Важность"
    };
}
