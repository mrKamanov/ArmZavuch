using CommunityToolkit.Mvvm.ComponentModel;

namespace ArmZavuch.Models;

/// <summary>Чекбокс класса в карточке сотрудника (подсказка для расписания).</summary>
public sealed partial class ClassPreferenceItem : ObservableObject
{
    public int ClassId { get; init; }
    public string DisplayName { get; init; } = "";

    [ObservableProperty] private bool _isSelected;
}
