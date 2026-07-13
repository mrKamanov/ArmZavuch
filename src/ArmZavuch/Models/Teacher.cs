namespace ArmZavuch.Models;

/// <summary>Сотрудник школы: педагог или вспомогательный персонал (ТЗ §3).</summary>
public sealed class Teacher : SelectableEntity
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string TeacherType { get; set; } = TeacherTypes.Subject;
    public string? JobTitle { get; set; }
    public int MaxLoadHours { get; set; } = 18;
    public int? RoomId { get; set; }
    public string? PrimarySubject { get; set; }

    /// <summary>Дополнительные предметы из teacher_subjects (profile Secondary).</summary>
    public List<string> SecondarySubjects { get; set; } = [];

    /// <summary>Первая строка доп. предметов для экспорта и совместимости.</summary>
    public string? SecondarySubject
    {
        get => SecondarySubjects.Count == 0 ? null : string.Join(", ", SecondarySubjects);
        set => SecondarySubjects = string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
    }
    public int? HomeroomClassId { get; set; }
    public string? HomeroomClass { get; set; }
    public string? Phone { get; set; }
    public string? ContactUrl { get; set; }
    public string? ContactNote { get; set; }
    public bool WorksWithFirstGrade { get; set; }

    /// <summary>Классы, с которыми педагог обычно работает (подсказка, не ограничение).</summary>
    public List<int> PreferredClassIds { get; set; } = [];

    public string PreferredClassesDisplay { get; set; } = "";

    /// <summary>Строки нагрузки из справочника, которые ведёт педагог.</summary>
    public List<TeacherCurriculumAssignment> CurriculumAssignments { get; set; } = [];

    public string CurriculumAssignmentsDisplay =>
        CurriculumAssignments.Count == 0
            ? ""
            : string.Join(", ", CurriculumAssignments
                .OrderBy(x => x.ClassName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.SubjectName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.WeekParity, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.DisplayLine));

    public string SchedulingHintsDisplay
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(HomeroomClass))
                parts.Add($"кл.рук. {HomeroomClass}");
            if (WorksWithFirstGrade)
                parts.Add("1 кл.");
            if (!string.IsNullOrWhiteSpace(PreferredClassesDisplay))
                parts.Add(PreferredClassesDisplay);
            return parts.Count == 0 ? "" : string.Join("; ", parts);
        }
    }

    public string TypeDisplay => TeacherTypes.ToDisplay(TeacherType);

    public string RoleDisplay => string.IsNullOrWhiteSpace(JobTitle)
        ? TypeDisplay
        : $"{TypeDisplay} · {JobTitle}";

    public string ContactLine
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Phone))
                parts.Add(Phone);
            if (!string.IsNullOrWhiteSpace(ContactUrl))
                parts.Add(ContactUrl);
            if (!string.IsNullOrWhiteSpace(ContactNote))
                parts.Add(ContactNote);
            return parts.Count == 0 ? "" : string.Join(" · ", parts);
        }
    }
}
