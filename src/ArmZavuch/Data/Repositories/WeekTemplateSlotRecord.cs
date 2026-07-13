namespace ArmZavuch.Data.Repositories;

public sealed class WeekTemplateSlotRecord
{
    public int SlotId { get; init; }
    public int? SubjectId { get; init; }
    public int? TeacherId { get; init; }
    public int? RoomId { get; init; }
    public bool IsAnchored { get; init; }
}
