using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArmZavuch.Models;

/// <summary>Нагрузка одного класса: строки таблицы с назначением педагогов.</summary>
public sealed partial class CurriculumClassGroup : ObservableObject
{
    public int ClassId { get; init; }
    public string ClassName { get; init; } = "";
    public string HeaderText { get; init; } = "";
    public int ItemCount { get; init; }
    public ObservableCollection<CurriculumGridRow> Rows { get; } = [];

    [ObservableProperty] private bool _isExpanded = true;
}
