namespace ArmZavuch.Models;

/// <summary>Кандидат на замену со скорингом (ТЗ §5).</summary>
public sealed class SubstitutionCandidate
{
    public int TeacherId { get; set; }
    public string TeacherName { get; set; } = "";
    public int Score { get; set; }
    public string CurrentLocation { get; set; } = "";
    public string? FreesAt { get; set; }
    public bool HasLateRisk { get; set; }
    public bool HasShuttleWarning { get; set; }
    public string ShuttleWarningText { get; set; } = "";
    public int CrossBuildingTransitionCount { get; set; }
    public bool HasLoadWarning { get; set; }
    public int LessonsToday { get; set; }
    public string? Phone { get; set; }
    public string? ContactUrl { get; set; }
    public string? RoleHint { get; set; }
    public bool IsDayFinished { get; set; }
    public string? LastLessonEndTime { get; set; }
    public string? StillWorkingHint { get; set; }

    public string? DayFinishedHint =>
        IsDayFinished && !string.IsNullOrWhiteSpace(LastLessonEndTime)
            ? $"Уроки закончились в {LastLessonEndTime}"
            : null;

    public string? AvailabilityHint => StillWorkingHint ?? DayFinishedHint;
}
