namespace ArmZavuch.Models;

/// <summary>Нагрузка: класс + предмет + часы в неделю; балл Сивкова — на строку (ТЗ §3).</summary>
public sealed class CurriculumItem : SelectableEntity
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public string ClassName { get; set; } = "";
    public int ClassGrade { get; set; }
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = "";
    public double SubjectDifficultyScore { get; set; }
    public double HoursPerWeek { get; set; }
    public bool HasSubgroups { get; set; }
    public string WeekParity { get; set; } = CurriculumWeekParity.EveryWeek;

    public string WeekParityDisplay => CurriculumWeekParity.ToDisplay(WeekParity);

    public string PaletteLabel => WeekParity == CurriculumWeekParity.EveryWeek
        ? $"{ClassName} · {SubjectName}"
        : $"{ClassName} · {SubjectName} ({WeekParityDisplay})";

    public string GradeGroupTitle => ClassGrade > 0 ? $"{ClassGrade} класс" : "Без параллели";

    /// <summary>Заголовок карточки с учётом текущей группировки палитры.</summary>
    public string PalettePrimaryLine { get; set; } = "";

    /// <summary>Уже разложено в текущем шаблоне (число уроков/часов).</summary>
    public double ScheduledHours { get; private set; }

    /// <summary>Сколько ещё нужно разложить в шаблон.</summary>
    public double RemainingHours => Math.Max(0, HoursPerWeek - ScheduledHours);

    public bool IsFullyScheduled => RemainingHours <= 0.01;

    public string RemainingHoursDisplay =>
        Math.Abs(RemainingHours - Math.Round(RemainingHours)) < 0.01
            ? $"{RemainingHours:0}"
            : $"{RemainingHours:0.#}";

    public string PaletteHoursLine => $"из {HoursPerWeek:0.#} ч/нед";

    public void SetScheduleCounts(double scheduledHours)
    {
        if (Math.Abs(ScheduledHours - scheduledHours) < 0.001)
            return;

        ScheduledHours = scheduledHours;
        OnPropertyChanged(nameof(ScheduledHours));
        OnPropertyChanged(nameof(RemainingHours));
        OnPropertyChanged(nameof(IsFullyScheduled));
        OnPropertyChanged(nameof(RemainingHoursDisplay));
        OnPropertyChanged(nameof(PaletteHoursLine));
    }
}

