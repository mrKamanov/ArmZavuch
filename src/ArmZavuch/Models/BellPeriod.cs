using ArmZavuch.Services.Schedule;

namespace ArmZavuch.Models;

/// <summary>Урок, перемена или дин. пауза в шаблоне звонков (ТЗ §3).</summary>
public sealed class BellPeriod : SelectableEntity
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public string TemplateName { get; set; } = "";
    public int TemplateGradeFrom { get; set; } = 1;
    public int TemplateGradeTo { get; set; } = 11;
    public int LessonNumber { get; set; }
    public int Shift { get; set; } = 1;
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
    public string PeriodKind { get; set; } = BellPeriodKinds.Lesson;

    public string PeriodKindDisplay => BellPeriodKinds.ToDisplay(PeriodKind);
    public string TemplateDisplayName => BellTemplateNaming.ToDisplay(TemplateName);
    public string GradeRangeDisplay => TemplateGradeFrom == TemplateGradeTo
        ? $"{TemplateGradeFrom} кл."
        : $"{TemplateGradeFrom}–{TemplateGradeTo} кл.";

    public int GradeRangeWidth => TemplateGradeTo - TemplateGradeFrom;

    public bool MatchesGrade(int grade) => grade >= TemplateGradeFrom && grade <= TemplateGradeTo;

    public string SlotLabel => PeriodKind switch
    {
        BellPeriodKinds.Lesson => $"Урок {LessonNumber}",
        BellPeriodKinds.DynamicPause => $"Дин. пауза после {LessonNumber}-го",
        BellPeriodKinds.Break => LessonNumber <= 0 ? "Перемена" : $"Перемена после {LessonNumber}-го",
        _ => PeriodKind
    };

    public string DurationDisplay => BellTime.FormatDuration(StartTime, EndTime);

    public string DurationMinutesDisplay
    {
        get
        {
            var minutes = BellTime.TryDurationMinutes(StartTime, EndTime);
            return minutes.HasValue ? minutes.Value.ToString() : "";
        }
    }

    public string TimeRangeDisplay => BellTime.FormatRange(StartTime, EndTime);
}
