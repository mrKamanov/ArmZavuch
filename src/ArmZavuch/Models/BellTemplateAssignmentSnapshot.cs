namespace ArmZavuch.Models;

/// <summary>Снимок назначений звонков для построения сеток. Вход: классы и настройки. Выход: шаблон на класс.</summary>
public sealed class BellTemplateAssignmentSnapshot
{
    public static BellTemplateAssignmentSnapshot Fallback { get; } = new()
    {
        TemplateByClassId = new Dictionary<int, string>(),
        CustomClassIds = new HashSet<int>(),
        DefaultGrade1 = BellTemplateNaming.Grade1,
        DefaultGrade1SecondHalf = BellTemplateNaming.Grade1SecondHalf,
        DefaultShift1 = BellTemplateNaming.Standard,
        DefaultShift2 = BellTemplateNaming.SecondShift,
        AsOfDate = null
    };

    public required IReadOnlyDictionary<int, string> TemplateByClassId { get; init; }
    public required IReadOnlySet<int> CustomClassIds { get; init; }
    public required string DefaultGrade1 { get; init; }
    public required string DefaultGrade1SecondHalf { get; init; }
    public required string DefaultShift1 { get; init; }
    public required string DefaultShift2 { get; init; }
    public DateOnly? AsOfDate { get; init; }

    public string GetTemplateName(SchoolClass cls) =>
        TemplateByClassId.TryGetValue(cls.Id, out var name) ? name : ResolveDefault(cls);

    public string GetTemplateName(int classId, int grade, int shift) =>
        TemplateByClassId.TryGetValue(classId, out var name) ? name : ResolveDefault(grade, shift);

    public string GetTemplateName(LessonSlot slot) =>
        !string.IsNullOrWhiteSpace(slot.BellTemplateName)
            ? slot.BellTemplateName.Trim()
            : GetTemplateName(slot.ClassId, slot.ClassGrade, slot.ClassShift);

    public bool IsCustomClass(SchoolClass cls) => CustomClassIds.Contains(cls.Id);

    public string ResolveDefault(SchoolClass cls) => ResolveDefault(cls.Grade, cls.Shift);

    public string ResolveDefault(int grade, int shift)
    {
        if (grade == 1)
        {
            if (AsOfDate.HasValue && Grade1BellSemesterRules.UseSecondHalfTemplate(AsOfDate.Value))
                return DefaultGrade1SecondHalf;
            return DefaultGrade1;
        }

        return shift == 2 ? DefaultShift2 : DefaultShift1;
    }

    public string ResolveShiftStandardTemplateName(int shift) =>
        shift == 2 ? DefaultShift2 : DefaultShift1;
}
