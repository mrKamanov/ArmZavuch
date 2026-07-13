namespace ArmZavuch.Models;

/// <summary>Уровень общего образования (ФОП).</summary>
public enum EducationLevel
{
    Noo,
    Ooo,
    Soo
}

public static class EducationLevelHelper
{
    public static EducationLevel FromGrade(int grade) => grade switch
    {
        <= 4 => EducationLevel.Noo,
        <= 9 => EducationLevel.Ooo,
        _ => EducationLevel.Soo
    };

    public static string ToDisplay(EducationLevel level) => level switch
    {
        EducationLevel.Noo => "НОО (1–4)",
        EducationLevel.Ooo => "ООО (5–9)",
        _ => "СОО (10–11)"
    };
}
