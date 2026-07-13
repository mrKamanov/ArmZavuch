namespace ArmZavuch.Models;

/// <summary>Шаблон нагрузки для диапазона параллелей (ТЗ §3).</summary>
public sealed class CurriculumTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int GradeFrom { get; set; }
    public int GradeTo { get; set; }
    public bool IsBuiltIn { get; set; }
    public int SortOrder { get; set; }
    public List<CurriculumTemplateItem> Items { get; set; } = [];

    public string GradeRangeDisplay => GradeFrom == GradeTo
        ? $"{GradeFrom} класс"
        : $"{GradeFrom}–{GradeTo} классы";

    public string KindLabel => IsBuiltIn ? "встроенный" : "свой";

    /// <summary>Строки для параллели шаблона: фильтр по grade и без дублей предмет+неделя.</summary>
    public IReadOnlyList<CurriculumTemplateItem> ResolveItemsForGrade(int? grade = null)
    {
        var targetGrade = grade ?? GradeFrom;
        return Items
            .Where(i => i.AppliesToGrade(targetGrade))
            .GroupBy(i => $"{i.SubjectName.Trim()}|{i.WeekParity}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(i => i.SubjectName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

/// <summary>Строка шаблона нагрузки: предмет по имени и параметры для класса.</summary>
public sealed class CurriculumTemplateItem
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public string SubjectName { get; set; } = "";
    public double HoursPerWeek { get; set; }
    public double DifficultyScore { get; set; }
    public bool HasSubgroups { get; set; }
    public string WeekParity { get; set; } = CurriculumWeekParity.EveryWeek;

    public string WeekParityDisplay => CurriculumWeekParity.ToDisplay(WeekParity);
    /// <summary>0 — для всего диапазона шаблона.</summary>
    public int ItemGradeFrom { get; set; }
    public int ItemGradeTo { get; set; }

    public bool AppliesToGrade(int grade) =>
        (ItemGradeFrom <= 0 || grade >= ItemGradeFrom)
        && (ItemGradeTo <= 0 || grade <= ItemGradeTo);
}
