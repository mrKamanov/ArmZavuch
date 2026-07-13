namespace ArmZavuch.Models;

/// <summary>Кабинет (ТЗ §3).</summary>
public sealed class Room : SelectableEntity
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public int BuildingId { get; set; }
    public string BuildingName { get; set; } = "";
    public string BuildingColorHex { get; set; } = "#94A3B8";
    public int Capacity { get; set; } = 30;
    public string RoomKind { get; set; } = RoomKinds.RegularStorage;
    public string RoomKindDisplay => RoomKinds.ToDisplay(RoomKind);
    /// <summary>Спортзал: допускается несколько классов одновременно (мягкое предупреждение, не блок).</summary>
    public bool AllowsParallelGroups { get; set; }
    public int? AssignedTeacherId { get; set; }
    public string? AssignedTeacherName { get; set; }

    public string ParallelGroupsDisplay => AllowsParallelGroups ? "да" : "";
}
