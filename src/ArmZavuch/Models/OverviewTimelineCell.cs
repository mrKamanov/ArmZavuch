namespace ArmZavuch.Models;

/// <summary>Один урок в вертикальной шкале дня.</summary>
public sealed class OverviewTimelineCell
{
    public int LessonNumber { get; init; }
    public bool IsDynamicPauseColumn { get; init; }
    public bool IsBreakColumn { get; init; }
    public string TimeLabel { get; init; } = "";
    public List<OverviewTimelinePart> Parts { get; init; } = [];
    public int CellHeight { get; set; } = OverviewTimelineCellHeights.Min;
    public bool HasMultipleParts => Parts.Count > 1;
    public int? ClassId { get; init; }
    public int? TeacherId { get; init; }
    public int? RoomId { get; init; }
    public string? BuildingName { get; init; }
    /// <summary>Смена колонки (1 или 2) — подсветка II смены в кабинетах.</summary>
    public int ColumnClassShift { get; init; } = 1;
    public bool IsSecondShiftColumn => ColumnClassShift >= 2;
    /// <summary>Первая ячейка II смены в вертикальной шкале дня.</summary>
    public bool IsShiftBoundaryRow { get; init; }

    public bool HasContent => Parts.Count > 0;

    public string ToolTipText => HasContent
        ? string.Join("\n\n", Parts.Select(p => p.ToolTipText))
        : TimeLabel;
}

public static class OverviewTimelineCellHeights
{
    public const int Min = 58;
}
