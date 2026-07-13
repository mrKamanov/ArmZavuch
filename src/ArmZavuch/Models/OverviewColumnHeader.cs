namespace ArmZavuch.Models;

/// <summary>Заголовок столбца дня недели в сводке.</summary>
public sealed class OverviewColumnHeader
{
    public string Label { get; init; } = "";
    public int DayOfWeek { get; init; }
    public string HeaderBackground => OverviewVisualStyle.DayHeaderBackground(DayOfWeek);
    public string HeaderBorderBrush => OverviewVisualStyle.DayColumnBorder(DayOfWeek);
}
