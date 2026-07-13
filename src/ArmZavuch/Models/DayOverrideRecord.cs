namespace ArmZavuch.Models;

/// <summary>Суточная правка расписания (слой DayOverride).</summary>
public sealed class DayOverrideRecord
{
    public int Id { get; set; }
    public string Date { get; set; } = "";
    public string OverrideType { get; set; } = "";
    public int? ClassId { get; set; }
    public int? LessonNumber { get; set; }
    public int? TeacherId { get; set; }
    public int? ReplacementTeacherId { get; set; }
    public int? RoomId { get; set; }
    public bool ClearRoom { get; set; }
    public int? BellTemplateId { get; set; }
    public int? TargetClassId { get; set; }
    public int? TargetLessonNumber { get; set; }
    public string? Note { get; set; }

    public string DisplayLine => OverrideType switch
    {
        "ShortenedDay" => DayBellAdjustment.TryParse(Note) is { } adjustment
            ? adjustment.DisplayLine
            : $"Сокращённый день · шаблон звонков «{Note ?? "?"}»",
        "SwapSlots" => $"Перестановка: урок {LessonNumber} ↔ урок {TargetLessonNumber}",
        "MoveLesson" => $"Перенос: урок {LessonNumber} → {TargetLessonNumber}",
        "ChangeSlot" => $"Изменение урока {LessonNumber}" + (Note is not null ? $": {Note}" : ""),
        "TeacherAbsent" => "Отсутствие учителя",
        "CancelLesson" => $"Отмена урока {LessonNumber}",
        "Substitution" => $"Замена на уроке {LessonNumber}",
        "DayNote" => "Заметка на день",
        _ => OverrideType
    };
}
