namespace ArmZavuch.Models;

/// <summary>Карточка шаблона звонков для списка в справочниках.</summary>
public sealed class BellTemplateCard
{
    public int TemplateId { get; init; }
    public string Name { get; init; } = "";
    public int GradeFrom { get; init; }
    public int GradeTo { get; init; }
    public int LessonCount { get; init; }
    public int PeriodCount { get; init; }
    public bool HasShift1 { get; init; }
    public bool HasShift2 { get; init; }
    public string? DayRangeDisplay { get; init; }
    public string DefaultUsageDisplay { get; init; } = "";

    public string DisplayName => BellTemplateNaming.ToDisplay(Name);

    public string GradeRangeDisplay => GradeFrom == GradeTo
        ? $"{GradeFrom} класс"
        : $"{GradeFrom}–{GradeTo} классы";

    public string ShiftSummary => HasShift1 && HasShift2
        ? "1 и 2 смена"
        : HasShift2 ? "2 смена" : "1 смена";

    public string SummaryLine => LessonCount > 0
        ? $"{LessonCount} урок(ов) · {DayRangeDisplay ?? "—"}"
        : "Пустой шаблон — добавьте уроки";
}
