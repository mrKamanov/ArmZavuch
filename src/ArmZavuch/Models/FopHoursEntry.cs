namespace ArmZavuch.Models;

/// <summary>Эталонный объём часов по ФОП для параллели.</summary>
public sealed class FopHoursEntry
{
    public string SubjectName { get; init; } = "";
    public int Grade { get; init; }
    public double HoursPerWeek { get; init; }
    public EducationLevel Level { get; init; }
    public string PlanVariant { get; init; } = "";
    public double DifficultyScore { get; init; } = 1.0;
}
