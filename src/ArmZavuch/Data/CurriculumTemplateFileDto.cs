namespace ArmZavuch.Data;

using ArmZavuch.Models;

/// <summary>Формат JSON встроенных шаблонов нагрузки. Вход: файл. Выход: seed в БД.</summary>
internal sealed class CurriculumTemplateFileDto
{
    public string Name { get; set; } = "";
    public int GradeFrom { get; set; }
    public int GradeTo { get; set; }
    public bool IsBuiltIn { get; set; }
    public List<CurriculumTemplateFileItemDto> Items { get; set; } = [];
}

internal sealed class CurriculumTemplateFileItemDto
{
    public string SubjectName { get; set; } = "";
    public double HoursPerWeek { get; set; }
    public double DifficultyScore { get; set; }
    public bool HasSubgroups { get; set; }
    public string WeekParity { get; set; } = CurriculumWeekParity.EveryWeek;
    public int GradeFrom { get; set; }
    public int GradeTo { get; set; }
}
