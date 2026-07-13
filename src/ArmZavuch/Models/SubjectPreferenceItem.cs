using CommunityToolkit.Mvvm.ComponentModel;

namespace ArmZavuch.Models;

/// <summary>Чекбокс предмета в карточке сотрудника (дополнительный профиль).</summary>
public sealed partial class SubjectPreferenceItem : ObservableObject
{
    public int SubjectId { get; init; }
    public string DisplayName { get; init; } = "";

    [ObservableProperty] private bool _isSelected;
}
