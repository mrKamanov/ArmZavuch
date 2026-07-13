namespace ArmZavuch.Models;

/// <summary>Типы сотрудников и отображение в UI.</summary>
public static class TeacherTypes
{
    public const string Subject = "Subject";
    public const string Primary = "Primary";
    public const string Auxiliary = "Auxiliary";

    public static readonly string[] All = [Subject, Primary, Auxiliary];

    public static string ToDisplay(string type) => type switch
    {
        Primary => "Начальные классы",
        Auxiliary => "Вспомогательный",
        _ => "Предметник"
    };

    public static string FromDisplay(string display)
    {
        if (display.Contains("Началь", StringComparison.OrdinalIgnoreCase))
            return Primary;
        if (display.Contains("Вспом", StringComparison.OrdinalIgnoreCase)
            || display.Contains("логопед", StringComparison.OrdinalIgnoreCase))
            return Auxiliary;
        return Subject;
    }
}
