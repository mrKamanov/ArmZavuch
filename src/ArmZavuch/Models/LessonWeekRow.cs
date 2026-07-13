using System.Collections.ObjectModel;

namespace ArmZavuch.Models;

/// <summary>Строка недельной сетки — один урок, ячейки по дням Пн–Сб.</summary>
public sealed class LessonWeekRow
{
    public int LessonNumber { get; set; }
    public bool IsDynamicPauseColumn { get; set; }
    public bool IsBreakColumn { get; set; }
    public string RowTitle { get; set; } = "";
    public string LessonTitle => string.IsNullOrWhiteSpace(RowTitle)
        ? (IsDynamicPauseColumn ? "Дин. пауза" : $"Урок {LessonNumber}")
        : RowTitle;
    public string BellTimeDisplay { get; set; } = "";
    public bool HasBellTime => !string.IsNullOrWhiteSpace(BellTimeDisplay);
    public ObservableCollection<GridCell> Days { get; } = [];
}
