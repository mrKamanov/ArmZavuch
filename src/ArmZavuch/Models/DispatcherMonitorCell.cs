using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ArmZavuch.Models;

/// <summary>Ячейка монитора дня (урок, пауза, перемена или окно).</summary>
public sealed class DispatcherMonitorCell : INotifyPropertyChanged
{
    public const string KindLesson = "Lesson";
    public const string KindDynamicPause = "DynamicPause";
    public const string KindBreak = "Break";
    public const string KindWindow = "Window";
    public const string KindEmpty = "Empty";

    public int ColumnIndex { get; init; }
    public int LessonNumber { get; init; }
    public string ColumnKind { get; init; } = KindEmpty;
    public string TimeLabel { get; init; } = "";
    public bool HasLesson { get; init; }
    public bool IsWindow { get; init; }
    public string PrimaryLine { get; init; } = "";
    public string SecondaryLine { get; init; } = "";
    public string StatusLine { get; init; } = "";
    public string BuildingColorHex { get; init; } = "#CBD5E1";
    public string StatusKind { get; init; } = "Normal";
    public string ToolTip { get; init; } = "";
    public LessonSlot? Lesson { get; init; }
    /// <summary>Класс строки (для пустых ячеек и выделения).</summary>
    public int ClassId { get; init; }
    /// <summary>Смена колонки (1 или 2) — для подсветки II смены в здании.</summary>
    public int ColumnClassShift { get; init; } = 1;
    public bool IsSecondShiftColumn => ColumnClassShift >= 2;

    private bool _hasConflict;
    public bool HasConflict
    {
        get => _hasConflict;
        set
        {
            if (_hasConflict == value)
                return;
            _hasConflict = value;
            OnPropertyChanged();
        }
    }

    private string _conflictHint = "";
    public string ConflictHint
    {
        get => _conflictHint;
        set
        {
            if (_conflictHint == value)
                return;
            _conflictHint = value;
            OnPropertyChanged();
        }
    }

    private bool _hasRoomSharedWarning;
    public bool HasRoomSharedWarning
    {
        get => _hasRoomSharedWarning;
        set
        {
            if (_hasRoomSharedWarning == value)
                return;
            _hasRoomSharedWarning = value;
            OnPropertyChanged();
        }
    }

    private string _roomSharedHint = "";
    public string RoomSharedHint
    {
        get => _roomSharedHint;
        set
        {
            if (_roomSharedHint == value)
                return;
            _roomSharedHint = value;
            OnPropertyChanged();
        }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class DispatcherMonitorSection
{
    public string Title { get; init; } = "";
    public string SubTitle { get; init; } = "";
    public bool IsShiftHeader { get; init; }
    public IList<DispatcherMonitorColumn> Columns { get; init; } = [];
    public IList<DispatcherMonitorRow> Rows { get; init; } = [];
}

public sealed class DispatcherMonitorRow : INotifyPropertyChanged
{
    public int ClassId { get; init; }
    public string Label { get; init; } = "";
    public string SubLabel { get; init; } = "";
    public string BuildingColorHex { get; init; } = "";
    public IList<DispatcherMonitorCell> Cells { get; init; } = [];

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class DispatcherMonitorColumn
{
    public int Index { get; init; }
    public int Number { get; init; }
    public string Title { get; init; } = "";
    public string Time { get; init; } = "";
    public string ColumnKind { get; init; } = DispatcherMonitorCell.KindLesson;
    public int StartMinutes { get; init; }
    public int EndMinutes { get; init; }
    public bool IsNow { get; set; }
    public int ClassShift { get; init; } = 1;
    public bool IsSecondShift => ClassShift >= 2;
    /// <summary>Первая колонка II смены — вертикальный разделитель слева.</summary>
    public bool IsShiftBoundary { get; init; }
}

public sealed class DispatcherStatItem
{
    public string Label { get; init; } = "";
    public string Value { get; init; } = "";
    public string Hint { get; init; } = "";
    public IReadOnlyList<string> DetailLines { get; init; } = [];
    public double MinWidth { get; init; } = 110;
    public string AccentBackground { get; init; } = "#F1F5F9";
    public string AccentForeground { get; init; } = "#0F172A";
}

public static class DispatcherMonitorModes
{
    public const string Teachers = "Teachers";
    public const string Classes = "Classes";
    public const string Buildings = "Buildings";
}

public static class DispatcherSections
{
    public const int Monitor = 0;
    public const int Replacements = 1;
    public const int DayEditor = 2;
}

public static class DispatcherMonitorLayout
{
    public const double ColumnWidth = 152;
    public const double CellHeight = 104;
    public const double RowHeaderWidth = 148;
}
