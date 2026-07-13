namespace ArmZavuch.Models;

/// <summary>Одна подгруппа (или единственный урок) в ячейке сводки.</summary>
public sealed class OverviewTimelinePart
{
    public string SubgroupLabel { get; init; } = "";
    public string SubjectLine { get; init; } = "";
    public string TeacherLine { get; init; } = "";
    public string RoomLine { get; init; } = "";
    public string RoomBuildingColorHex { get; init; } = "#94A3B8";
    public string PrimaryLine { get; init; } = "";
    public string SecondaryLine { get; init; } = "";
    public int? ClassId { get; init; }
    public int? TeacherId { get; init; }
    public int? RoomId { get; init; }
    public string? BuildingName { get; init; }

    public bool HasRoomLine => !string.IsNullOrWhiteSpace(RoomLine);

    public string ToolTipText
    {
        get
        {
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(SubgroupLabel))
                lines.Add($"Подгруппа {SubgroupLabel}");
            if (!string.IsNullOrWhiteSpace(SubjectLine))
                lines.Add(SubjectLine);
            if (!string.IsNullOrWhiteSpace(TeacherLine))
                lines.Add(TeacherLine);
            if (!string.IsNullOrWhiteSpace(RoomLine))
                lines.Add(RoomLine);
            return lines.Count == 0 ? "" : string.Join("\n", lines);
        }
    }
}
