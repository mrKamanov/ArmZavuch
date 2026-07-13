using System.Collections.ObjectModel;

namespace ArmZavuch.Models;

/// <summary>Вертикальная колонка одного дня недели в строке сводки.</summary>
public sealed class OverviewDayColumn
{
    public int DayOfWeek { get; init; }
    public string ColumnBackground => OverviewVisualStyle.DayColumnBackground(DayOfWeek);
    public string ColumnBorderBrush => OverviewVisualStyle.DayColumnBorder(DayOfWeek);
    public ObservableCollection<OverviewTimelineCell> Lessons { get; } = [];
}
