namespace ArmZavuch.Models;

/// <summary>Тип записи в шаблоне звонков.</summary>
public static class BellPeriodKinds
{
    public const string Lesson = "Lesson";
    public const string Break = "Break";
    public const string DynamicPause = "DynamicPause";

    public static readonly string[] All = [Lesson, Break, DynamicPause];

    public static string ToDisplay(string kind) => kind switch
    {
        Lesson => "Урок",
        Break => "Перемена",
        DynamicPause => "Дин. пауза",
        _ => kind
    };

    public static bool IsLesson(string kind) =>
        string.Equals(kind, Lesson, StringComparison.Ordinal);

    public static string Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "lesson" or "урок" => Lesson,
        "break" or "перемена" => Break,
        "dynamicpause" or "дин. пауза" or "динамическая пауза" or "дин.пауза" => DynamicPause,
        _ => Lesson
    };
}
