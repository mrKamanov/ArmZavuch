namespace ArmZavuch.Models;

/// <summary>Группа столбцов классов одного здания в шапке сетки.</summary>
public sealed class ConstructorBuildingBand
{
    public string BuildingName { get; init; } = "";
    public string BuildingColorHex { get; init; } = "#94A3B8";
    public int SpanCount { get; init; }
    public double BandWidth => SpanCount * ConstructorGridLayout.ColumnWidth + Math.Max(0, SpanCount - 1) * 4;
}
