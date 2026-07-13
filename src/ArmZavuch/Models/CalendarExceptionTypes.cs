namespace ArmZavuch.Models;

/// <summary>Тип исключения учебного календаря.</summary>
public static class CalendarExceptionTypes
{
    public const string Holiday = "Holiday";
    public const string Vacation = "Vacation";
    public const string Compensation = "Compensation";

    public static string ToDisplay(string type) => type switch
    {
        Vacation => "Каникулы",
        Compensation => "Компенсационная суббота",
        _ => "Праздник / выходной"
    };
}
