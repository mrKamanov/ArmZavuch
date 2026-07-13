namespace ArmZavuch.Models;

/// <summary>Источник записи об отсутствии: анкета, диспетчерская или ручной ввод.</summary>
public static class AbsenceSources
{
    public const string Profile = "profile";
    public const string Dispatcher = "dispatcher";
    public const string Manual = "manual";

    public static string ToDisplay(string source) => source switch
    {
        Dispatcher => "диспетчерская",
        Manual => "вручную",
        _ => "анкета"
    };
}
