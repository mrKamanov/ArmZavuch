namespace ArmZavuch.Models;

/// <summary>Строка нагрузки, закреплённая за педагогом (из справочника curriculum).</summary>
public sealed class TeacherCurriculumAssignment
{
    public int CurriculumId { get; set; }
    public int ClassId { get; set; }
    public int SubjectId { get; set; }
    public string ClassName { get; set; } = "";
    public string SubjectName { get; set; } = "";
    public string WeekParity { get; set; } = CurriculumWeekParity.EveryWeek;
    public double HoursPerWeek { get; set; }

    public string DisplayLine => WeekParity == CurriculumWeekParity.EveryWeek
        ? $"{ClassName} · {SubjectName} ({FormatHours(HoursPerWeek)} ч/нед)"
        : $"{ClassName} · {SubjectName} · {CurriculumWeekParity.ToDisplay(WeekParity)} ({FormatHours(HoursPerWeek)} ч/нед)";

    private static string FormatHours(double hours) =>
        Math.Abs(hours - Math.Round(hours)) < 0.01 ? $"{hours:0}" : $"{hours:0.#}";
}
