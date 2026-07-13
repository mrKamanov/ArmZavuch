namespace ArmZavuch.Models;

/// <summary>Накладка: один учитель или один кабинет в одно время.</summary>
public sealed class ScheduleConflict
{
    public const string TeacherDoubleBook = "TEACHER_DOUBLE";
    public const string RoomDoubleBook = "ROOM_DOUBLE";
    public const string RoomSharedUse = "ROOM_SHARED";

    /// <summary>false — только предупреждение (например, несколько групп в спортзале).</summary>
    public bool IsBlocking { get; init; } = true;

    public string Kind { get; init; } = "";
    public int DayOfWeek { get; init; }
    public int ClassId { get; init; }
    public int LessonNumber { get; init; }
    public int TeacherId { get; init; }
    public int SlotIdA { get; init; }
    public int SlotIdB { get; init; }
    public string Message { get; init; } = "";
    public string ClassName { get; init; } = "";

    public IEnumerable<int> SlotIds => [SlotIdA, SlotIdB];
}
