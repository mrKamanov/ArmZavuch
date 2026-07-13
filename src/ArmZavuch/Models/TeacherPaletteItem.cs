using ArmZavuch.Services.Schedule;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArmZavuch.Models;

/// <summary>Карточка педагога в палитре конструктора: группа, компактные подсказки, нагрузка и факт в сетке.</summary>
public sealed partial class TeacherPaletteItem : ObservableObject
{
    public required Teacher Teacher { get; init; }
    public required string GroupName { get; init; }
    public int PlannedHours { get; private set; }
    public bool IsPlannedFromCurriculum { get; private set; }

    [ObservableProperty]
    private int _scheduledCount;

    public SubjectAccentColors.Accent SubjectAccent => SubjectAccentColors.Resolve(GroupName);
    public string AccentBorderHex => SubjectAccent.BorderHex;
    public string AccentBackgroundHex => SubjectAccent.BackgroundHex;
    public string SubjectBadgeText => SubjectAccent.BadgeText;

    public string PrimaryLine => Teacher.FullName;
    public string SecondaryLine => FormatCompactSchedulingHints(Teacher);
    public string? SecondaryLineToolTip
    {
        get
        {
            var full = Teacher.SchedulingHintsDisplay;
            return string.IsNullOrWhiteSpace(full) || full == SecondaryLine ? null : full;
        }
    }
    public string PlannedHoursText => PlannedHours.ToString();
    public string ScheduledCountText => ScheduledCount.ToString();
    public string PlannedHoursBrush => IsPlannedFromCurriculum ? "#2563EB" : "#64748B";
    public string ScheduledCountBrush =>
        TeacherPaletteMetrics.ResolveScheduledCountBrush(ScheduledCount, PlannedHours);

    [ObservableProperty]
    private bool _isSelectedForPlacement;

    public string PlacementSelectionMarker => IsSelectedForPlacement ? "▶ выбран" : "";

    partial void OnIsSelectedForPlacementChanged(bool value) =>
        OnPropertyChanged(nameof(PlacementSelectionMarker));

    partial void OnScheduledCountChanged(int value)
    {
        OnPropertyChanged(nameof(ScheduledCountText));
        OnPropertyChanged(nameof(ScheduledCountBrush));
        OnPropertyChanged(nameof(PlacementStatusText));
        OnPropertyChanged(nameof(IsFullyPlaced));
        OnPropertyChanged(nameof(HasPartialPlacement));
        OnPropertyChanged(nameof(CardBackgroundHex));
        OnPropertyChanged(nameof(CardBorderHex));
    }

    public void SetScheduleMetrics(int scheduledCount, int plannedHours, bool fromCurriculum)
    {
        PlannedHours = plannedHours;
        IsPlannedFromCurriculum = fromCurriculum;
        ScheduledCount = scheduledCount;
        OnPropertyChanged(nameof(ScheduledCount));
        OnPropertyChanged(nameof(ScheduledCountText));
        OnPropertyChanged(nameof(ScheduledCountBrush));
        OnPropertyChanged(nameof(PlacementStatusText));
        OnPropertyChanged(nameof(IsFullyPlaced));
        OnPropertyChanged(nameof(HasPartialPlacement));
        OnPropertyChanged(nameof(CardBackgroundHex));
        OnPropertyChanged(nameof(CardBorderHex));
        OnPropertyChanged(nameof(PlannedHoursText));
        OnPropertyChanged(nameof(PlannedHoursBrush));
        OnPropertyChanged(nameof(PlannedHoursToolTip));
    }

    public bool IsFullyPlaced => PlannedHours > 0 && ScheduledCount >= PlannedHours;

    public bool HasPartialPlacement => PlannedHours > 0 && ScheduledCount > 0 && ScheduledCount < PlannedHours;

    public string PlacementStatusText => PlannedHours <= 0
        ? ""
        : IsFullyPlaced
            ? "✓ разложен"
            : HasPartialPlacement
                ? $"в сетке {ScheduledCount}/{PlannedHours}"
                : "не разложен";

    public string CardBackgroundHex => IsFullyPlaced ? "#DCFCE7" : AccentBackgroundHex;

    public string CardBorderHex => IsFullyPlaced ? "#16A34A" : AccentBorderHex;

    public string PlannedHoursToolTip => IsPlannedFromCurriculum
        ? "Часы по отмеченной нагрузке в анкете"
        : PlannedHours == 0
            ? "Нагрузка не назначена — отметьте строки в анкете педагога"
            : "Часы из анкеты";

    private static string FormatCompactSchedulingHints(Teacher teacher)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(teacher.HomeroomClass))
            parts.Add($"кл.рук. {teacher.HomeroomClass}");
        if (teacher.WorksWithFirstGrade)
            parts.Add("1 кл.");

        if (!string.IsNullOrWhiteSpace(teacher.PreferredClassesDisplay))
        {
            var classes = teacher.PreferredClassesDisplay
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList();
            parts.Add(classes.Count <= 3
                ? string.Join(", ", classes)
                : $"{string.Join(", ", classes.Take(2))} +{classes.Count - 2}");
        }

        return parts.Count == 0 ? "" : string.Join(" · ", parts);
    }
}
