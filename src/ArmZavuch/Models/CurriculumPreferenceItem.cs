using CommunityToolkit.Mvvm.ComponentModel;

namespace ArmZavuch.Models;

/// <summary>Чекбокс строки нагрузки в карточке сотрудника. П/г — до двух педагогов.</summary>
public sealed partial class CurriculumPreferenceItem : ObservableObject
{
    public int CurriculumId { get; init; }
    public string DisplayName { get; init; } = "";
    public string DetailsLine { get; init; } = "";
    public double HoursPerWeek { get; init; }
    public bool HasSubgroups { get; init; }
    public IReadOnlyList<string> OtherTeacherNames { get; init; } = [];

    public bool IsAssignedToOthers => OtherTeacherNames.Count > 0;

    public bool IsFullyOccupiedByOthers =>
        !IsSelected && (HasSubgroups
            ? OtherTeacherNames.Count >= 2
            : OtherTeacherNames.Count > 0);

    public bool IsAssignmentEnabled => IsSelected || !IsFullyOccupiedByOthers;

    public bool IsUnassignedHighlight => !IsSelected && OtherTeacherNames.Count == 0;

    public bool IsSubgroupSharedHighlight =>
        HasSubgroups && !IsSelected && OtherTeacherNames.Count == 1;

    public bool IsOthersHighlight => IsFullyOccupiedByOthers;

    public string AssignmentHint
    {
        get
        {
            if (HasSubgroups)
            {
                if (OtherTeacherNames.Count >= 2 && !IsSelected)
                    return $"Подгруппы · оба места заняты: {string.Join(", ", OtherTeacherNames)}";

                if (OtherTeacherNames.Count == 1 && !IsSelected)
                    return $"Подгруппы · также ведёт {OtherTeacherNames[0]}. Можно назначить второго педагога.";

                if (IsSelected && OtherTeacherNames.Count == 0)
                    return "Подгруппы · один педагог на обе п/г (по очереди или вместе)";

                if (OtherTeacherNames.Count > 0)
                    return $"Подгруппы · также ведут: {string.Join(", ", OtherTeacherNames)}";

                return IsSelected
                    ? "Подгруппы · ведёт этот педагог"
                    : "Подгруппы · никому не назначено";
            }

            if (OtherTeacherNames.Count > 0)
            {
                var names = string.Join(", ", OtherTeacherNames);
                return IsSelected
                    ? $"Также ведут: {names}"
                    : $"Уже ведёт: {names}";
            }

            return IsSelected ? "Ведёт этот педагог" : "Никому не назначено";
        }
    }

    [ObservableProperty] private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(AssignmentHint));
        OnPropertyChanged(nameof(IsUnassignedHighlight));
        OnPropertyChanged(nameof(IsSubgroupSharedHighlight));
        OnPropertyChanged(nameof(IsOthersHighlight));
        OnPropertyChanged(nameof(IsFullyOccupiedByOthers));
        OnPropertyChanged(nameof(IsAssignmentEnabled));
    }
}
