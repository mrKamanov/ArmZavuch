namespace ArmZavuch.Models;

/// <summary>Тип повторения нерабочего времени.</summary>
public static class UnavailabilityRecurrence
{
    public const string Weekly = "Weekly";
    public const string Once = "Once";
    public const string DateRange = "DateRange";

    public static readonly string[] All = [Weekly, Once, DateRange];

    public static string ToDisplay(string type) => type switch
    {
        Weekly => "Еженедельно",
        Once => "Разово",
        _ => "Период"
    };
}
