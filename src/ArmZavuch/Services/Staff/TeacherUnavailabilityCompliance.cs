using ArmZavuch.Models;

namespace ArmZavuch.Services.Staff;

/// <summary>
/// Нерабочее время учителя vs урок в недельном шаблоне.
/// Для «Еженедельно» — точное совпадение дня и номера урока.
/// </summary>
public static class TeacherUnavailabilityCompliance
{
    public static IEnumerable<string> GetTemplateWarnings(
        IEnumerable<TeacherUnavailability> blocks,
        int dayOfWeek,
        int lessonNumber,
        string? teacherName = null)
    {
        foreach (var block in blocks)
        {
            if (block.RecurrenceType != UnavailabilityRecurrence.Weekly)
                continue;
            if (block.DayOfWeek != dayOfWeek)
                continue;
            if (!OverlapsLesson(block, lessonNumber))
                continue;

            yield return FormatWarning(block, teacherName);
        }
    }

    public static bool HasWeeklyConflict(
        IEnumerable<TeacherUnavailability> blocks,
        int dayOfWeek,
        int lessonNumber) =>
        GetTemplateWarnings(blocks, dayOfWeek, lessonNumber).Any();

    private static bool OverlapsLesson(TeacherUnavailability block, int lessonNumber)
    {
        if (block.AllDay || block.LessonFrom is null)
            return true;

        var to = block.LessonTo ?? block.LessonFrom;
        return lessonNumber >= block.LessonFrom && lessonNumber <= to;
    }

    private static string FormatWarning(TeacherUnavailability block, string? teacherName)
    {
        var who = string.IsNullOrWhiteSpace(teacherName) ? "Учитель" : teacherName;
        var time = block.AllDay || block.LessonFrom is null
            ? "весь день"
            : block.LessonTo is null || block.LessonTo == block.LessonFrom
                ? $"урок {block.LessonFrom}"
                : $"уроки {block.LessonFrom}–{block.LessonTo}";
        var note = string.IsNullOrWhiteSpace(block.Note) ? "" : $" ({block.Note})";
        return $"{who}: нерабочее время — каждую {DayName(block.DayOfWeek)}, {time}{note}";
    }

    private static string DayName(int? dow) => dow switch
    {
        1 => "понедельник",
        2 => "вторник",
        3 => "среду",
        4 => "четверг",
        5 => "пятницу",
        6 => "субботу",
        _ => "?"
    };
}
