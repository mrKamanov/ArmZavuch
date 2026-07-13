using ArmZavuch.Models;

namespace ArmZavuch.Services.Staff;

/// <summary>
/// Сопоставление записей нерабочего времени с датой оперативки и слотами сетки.
/// Вход: блоки из анкеты сотрудника, дата дня, номер урока или колонка timeline.
/// </summary>
public static class TeacherUnavailabilityResolver
{
    public static bool MatchesDate(TeacherUnavailability block, DateOnly date)
    {
        var dateStr = date.ToString("yyyy-MM-dd");
        var dow = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
        return block.RecurrenceType switch
        {
            UnavailabilityRecurrence.Weekly =>
                block.DayOfWeek == dow
                && dateStr.CompareTo(block.StartDate) >= 0
                && (block.EndDate is null || dateStr.CompareTo(block.EndDate) <= 0),
            UnavailabilityRecurrence.Once =>
                block.StartDate == dateStr,
            _ =>
                dateStr.CompareTo(block.StartDate) >= 0
                && dateStr.CompareTo(block.EndDate ?? block.StartDate) <= 0
        };
    }

    public static IReadOnlyList<TeacherUnavailability> FilterForDate(
        IEnumerable<TeacherUnavailability> blocks,
        DateOnly date) =>
        blocks.Where(b => MatchesDate(b, date)).ToList();

    public static bool OverlapsLesson(TeacherUnavailability block, int lessonNumber)
    {
        if (block.AllDay || block.LessonFrom is null)
            return true;

        var to = block.LessonTo ?? block.LessonFrom;
        return lessonNumber >= block.LessonFrom && lessonNumber <= to;
    }

    public static bool OverlapsColumn(
        TeacherUnavailability block,
        string columnKind,
        ConstructorTimelineColumn column)
    {
        if (block.AllDay || block.LessonFrom is null)
            return true;

        foreach (var lessonNumber in ResolveColumnLessonNumbers(columnKind, column))
        {
            if (OverlapsLesson(block, lessonNumber))
                return true;
        }

        return false;
    }

    public static TeacherUnavailability? FindForColumn(
        IEnumerable<TeacherUnavailability> blocks,
        DateOnly date,
        string columnKind,
        ConstructorTimelineColumn column)
    {
        foreach (var block in FilterForDate(blocks, date))
        {
            if (OverlapsColumn(block, columnKind, column))
                return block;
        }

        return null;
    }

    public static string FormatDispatcherPrimary(TeacherUnavailability block) =>
        string.IsNullOrWhiteSpace(block.Note) ? "нерабочее" : block.Note.Trim();

    public static string FormatDispatcherSecondary(TeacherUnavailability block) =>
        block.AllDay || block.LessonFrom is null
            ? "весь день"
            : block.LessonTo is null || block.LessonTo == block.LessonFrom
                ? $"урок {block.LessonFrom}"
                : $"уроки {block.LessonFrom}–{block.LessonTo}";

    public static string FormatDispatcherToolTip(TeacherUnavailability block) =>
        $"{FormatDispatcherPrimary(block)} · {FormatDispatcherSecondary(block)}\n{block.DisplayText}";

    private static IEnumerable<int> ResolveColumnLessonNumbers(
        string columnKind,
        ConstructorTimelineColumn column)
    {
        if (columnKind is DispatcherMonitorCell.KindBreak or DispatcherMonitorCell.KindDynamicPause)
        {
            if (column.AfterLessonNumber > 0)
                yield return column.AfterLessonNumber;
            yield return column.AfterLessonNumber + 1;
        }
        else if (column.LessonNumber > 0)
        {
            yield return column.LessonNumber;
        }
    }
}
