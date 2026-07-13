namespace ArmZavuch.Models;

/// <summary>Точка горизонтальной диаграммы для сводки журнала.</summary>
public sealed class StaffBarChartPoint
{
    public string Label { get; init; } = "";
    public string Caption { get; init; } = "";
    public double BarPixelWidth { get; init; }
}
