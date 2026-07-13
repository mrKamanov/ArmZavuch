namespace ArmZavuch.Models;

/// <summary>Рекомендация/предупреждение о переходе учителя между зданиями.</summary>
public sealed class BuildingTransitionWarning
{
    public int TeacherId { get; init; }
    public string TeacherName { get; init; } = "";
    public LessonSlot EarlierLesson { get; init; } = null!;
    public LessonSlot LaterLesson { get; init; } = null!;
    public int RequiredMinutes { get; init; }
    public int? AvailableMinutes { get; init; }
    public bool RouteConfigured { get; init; }
    public bool IsTimeCritical { get; init; }
    public bool IsConsecutiveDifferentBuildings { get; init; }
    /// <summary>Достаточно времени на переход, но педагогу придётся ходить между корпусами.</summary>
    public bool IsShuttleReminder { get; init; }
    public string Message { get; init; } = "";

    public bool IsRecommendation => IsConsecutiveDifferentBuildings || IsTimeCritical || IsShuttleReminder;
}
