namespace ArmZavuch.Models;

/// <summary>Фильтр проваливания в сводном расписании.</summary>
public sealed class OverviewFilter
{
    public int? ClassId { get; set; }
    public int? TeacherId { get; set; }
    public int? RoomId { get; set; }
    public string? BuildingName { get; set; }

    public bool IsActive =>
        ClassId is not null || TeacherId is not null || RoomId is not null
        || !string.IsNullOrWhiteSpace(BuildingName);

    public void Clear()
    {
        ClassId = null;
        TeacherId = null;
        RoomId = null;
        BuildingName = null;
    }

    public OverviewFilter Clone() => new()
    {
        ClassId = ClassId,
        TeacherId = TeacherId,
        RoomId = RoomId,
        BuildingName = BuildingName
    };
}
