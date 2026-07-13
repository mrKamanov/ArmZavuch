using System.Collections.ObjectModel;

namespace ArmZavuch.Models;

/// <summary>Строка сетки — урок или дин. пауза, ячейки по классам.</summary>
public sealed class ConstructorLessonRow
{
    public ConstructorTimelineColumn Column { get; init; } = new();
    public string RowTitle { get; init; } = "";
    public string BellTimeDisplay { get; init; } = "";
    public bool IsDynamicPause => Column.IsDynamicPause;
    public bool IsBreak => Column.IsBreak;
    public ObservableCollection<GridCell> Cells { get; } = [];
}
