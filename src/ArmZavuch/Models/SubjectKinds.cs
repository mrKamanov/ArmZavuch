namespace ArmZavuch.Models;

/// <summary>Тип предмета в справочнике.</summary>
public static class SubjectKinds
{
    public const string Academic = "Academic";
    public const string Extracurricular = "Extracurricular";
    public const string Elective = "Elective";
    public const string Special = "Special";

    public static string ToDisplay(string kind) => kind switch
    {
        Extracurricular => "Внеурочное",
        Elective => "Факультатив",
        Special => "Особое",
        _ => "Учебное"
    };

    public static string Badge(string kind) => kind switch
    {
        Extracurricular => "внеур.",
        Elective => "фак.",
        Special => "особ.",
        _ => ""
    };
}
