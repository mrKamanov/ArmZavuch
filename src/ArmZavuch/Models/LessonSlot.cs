namespace ArmZavuch.Models;

/// <summary>Развёрнутый урок на конкретный день (шаблон + дата).</summary>
public sealed class LessonSlot
{
    public int SlotId { get; set; }
    public DateOnly Date { get; set; }
    public int LessonNumber { get; set; }
    /// <summary>Номер урока для отображения (с учётом дин. паузы).</summary>
    public int DisplayLessonNumber { get; set; }
    public string DisplayRowTitle { get; set; } = "";
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
    public string? BellTemplateName { get; set; }
    public int ClassId { get; set; }
    public string ClassName { get; set; } = "";
    public int ClassGrade { get; set; }
    public int ClassShift { get; set; } = 1;
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = "";
    public int TeacherId { get; set; }
    public string TeacherName { get; set; } = "";
    public int RoomId { get; set; }
    public string RoomNumber { get; set; } = "";
    public string BuildingName { get; set; } = "";
    public int SubgroupIndex { get; set; }
    public int DayOfWeek { get; set; }
    public bool IsCancelled { get; set; }
    public bool IsAnchored { get; set; }
    public int? ReplacementTeacherId { get; set; }
    public string? ReplacementTeacherName { get; set; }
    public string? AbsenceNote { get; set; }

    public bool NeedsReplacement =>
        !IsCancelled && ReplacementTeacherName?.Contains("замена", StringComparison.OrdinalIgnoreCase) == true;

    public bool HasAssignedReplacement =>
        !IsCancelled && ReplacementTeacherId.HasValue && !NeedsReplacement;

    public string TimeDisplay =>
        string.IsNullOrWhiteSpace(StartTime)
            ? "—"
            : string.IsNullOrWhiteSpace(EndTime)
                ? StartTime
                : $"{StartTime}–{EndTime}";

    public string DisplayLessonLabel =>
        !string.IsNullOrWhiteSpace(DisplayRowTitle)
            ? DisplayRowTitle
            : DisplayLessonNumber > 0
                ? DisplayLessonNumber.ToString()
                : LessonNumber.ToString();

    public string StatusLabel => IsCancelled
        ? "Отменён"
        : NeedsReplacement
            ? StaffStatusTypes.FormatPendingStatus(AbsenceNote)
            : HasAssignedReplacement
                ? "Замена назначена"
                : "";

    public string SwapDisplayLine =>
        $"{LessonNumber} урок · {ClassName} · {SubjectName} · {TeacherName}";
}
