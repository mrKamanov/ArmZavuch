namespace ArmZavuch.Models;

/// <summary>Нерабочее время: пары в вузе, совещания, методические дни.</summary>
public sealed class TeacherUnavailability
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public string RecurrenceType { get; set; } = UnavailabilityRecurrence.Weekly;
    public int? DayOfWeek { get; set; }
    public string StartDate { get; set; } = "";
    public string? EndDate { get; set; }
    public bool AllDay { get; set; } = true;
    public int? LessonFrom { get; set; }
    public int? LessonTo { get; set; }
    public string? Note { get; set; }

    public string DisplayText
    {
        get
        {
            var time = AllDay
                ? "весь день"
                : $"уроки {LessonFrom}–{LessonTo}";
            return RecurrenceType switch
            {
                UnavailabilityRecurrence.Weekly =>
                    $"Каждую {DayName(DayOfWeek)}: {time}",
                UnavailabilityRecurrence.Once =>
                    $"{StartDate}: {time}",
                _ => $"{StartDate} — {EndDate ?? StartDate}: {time}"
            } + (string.IsNullOrWhiteSpace(Note) ? "" : $" — {Note}");
        }
    }

    private static string DayName(int? dow) => dow switch
    {
        1 => "Пн",
        2 => "Вт",
        3 => "Ср",
        4 => "Чт",
        5 => "Пт",
        6 => "Сб",
        _ => "?"
    };
}
