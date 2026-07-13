namespace ArmZavuch.Models;

/// <summary>Ячейка сводной матрицы расписания (клик — проваливание).</summary>
public sealed class OverviewMatrixCell
{
    public string Text { get; init; } = "";
    public int? ClassId { get; init; }
    public int? TeacherId { get; init; }
    public int? RoomId { get; init; }
    public string? BuildingName { get; init; }

    public bool HasContent => !string.IsNullOrWhiteSpace(Text);
}
