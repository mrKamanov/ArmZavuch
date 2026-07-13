using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ArmZavuch.Models;

/// <summary>Блок сетки «все классы · день» с собственной шапкой столбцов.</summary>
public sealed class ConstructorDayGridSection : INotifyPropertyChanged
{
    public string Title { get; init; } = "";
    public string SubTitle { get; init; } = "";
    public string SectionKey { get; set; } = "";
    public int ShiftNumber { get; init; }
    public bool IsShiftHeader { get; init; }
    public bool IsFirstGradeSection { get; init; }
    public bool IsCustomBellTrack { get; init; }
    public bool IsTransposed { get; init; }
    public ObservableCollection<ConstructorTimelineColumn> Columns { get; } = [];
    public ObservableCollection<ClassGridRow> Rows { get; } = [];
    public IList<ConstructorBuildingBand> BuildingBands { get; init; } = [];
    public IList<ConstructorClassColumn> ClassColumns { get; init; } = [];
    public IList<ConstructorLessonRow> LessonRows { get; init; } = [];
    public int ColumnCount => IsTransposed ? ClassColumns.Count : Columns.Count;

    private bool _isCollapsed;
    private bool _isHiddenByParentShift;

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (_isCollapsed == value)
                return;
            _isCollapsed = value;
            Notify(nameof(IsCollapsed));
            Notify(nameof(IsGridVisible));
            Notify(nameof(CollapseToggleGlyph));
        }
    }

    public bool IsHiddenByParentShift
    {
        get => _isHiddenByParentShift;
        private set
        {
            if (_isHiddenByParentShift == value)
                return;
            _isHiddenByParentShift = value;
            Notify(nameof(IsHiddenByParentShift));
            Notify(nameof(IsGridVisible));
        }
    }

    public bool IsGridVisible => !IsShiftHeader && !IsHiddenByParentShift && !IsCollapsed;

    public string CollapseToggleGlyph => IsCollapsed ? "▸" : "▾";

    public void SetHiddenByParentShift(bool hidden) => IsHiddenByParentShift = hidden;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
