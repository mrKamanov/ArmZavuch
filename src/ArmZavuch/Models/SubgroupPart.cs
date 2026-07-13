namespace ArmZavuch.Models;

/// <summary>Половина ячейки с подгруппой (ТЗ §4: деление класса).</summary>
public sealed class SubgroupPart
{
    public int SubgroupIndex { get; set; }
    public int? SlotId { get; set; }
    public int? SubjectId { get; set; }
    public int? TeacherId { get; set; }
    public int? RoomId { get; set; }
    public string Line { get; set; } = "";
    public string SubjectName { get; set; } = "";
    public string TeacherName { get; set; } = "";
    public string RoomLine { get; set; } = "";
    public string RoomBuildingColorHex { get; set; } = "#94A3B8";
    public bool IsAnchored { get; set; }
    /// <summary>«[1]» / «[2]» в ячейке при делении класса.</summary>
    public string SubgroupLabel { get; set; } = "";
}
