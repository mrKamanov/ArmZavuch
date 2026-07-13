using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>Подсказки при добавлении строки в шаблон звонков (следующий слот, время).</summary>
public static class BellScheduleEditorHints
{
    private const int DefaultBreakMinutes = 10;
    private const int DefaultDynamicPauseMinutes = 15;
    private const int DefaultLessonMinutes = 40;

    public sealed record SlotSuggestion(
        string PeriodKind,
        int LessonNumber,
        string StartTime,
        string EndTime);

    public static SlotSuggestion SuggestNext(IReadOnlyList<BellPeriod> timeline, int gradeFrom)
    {
        if (timeline.Count == 0)
            return SuggestFirstLesson(gradeFrom);

        var ordered = BellScheduleTimelineSorter.OrderForDisplay(timeline).ToList();
        var last = ordered[^1];
        var lastEnd = last.EndTime;

        return last.PeriodKind switch
        {
            BellPeriodKinds.Lesson => SuggestAfterLesson(last, lastEnd),
            BellPeriodKinds.DynamicPause => SuggestAfterDynamicPause(last, lastEnd),
            BellPeriodKinds.Break => SuggestAfterBreak(last, lastEnd),
            _ => SuggestFirstLesson(gradeFrom)
        };
    }

    private static SlotSuggestion SuggestFirstLesson(int gradeFrom)
    {
        var minutes = gradeFrom <= 1 ? 35 : DefaultLessonMinutes;
        return new SlotSuggestion(
            BellPeriodKinds.Lesson,
            1,
            "08:30",
            BellTime.AddMinutes("08:30", minutes));
    }

    private static SlotSuggestion SuggestAfterLesson(BellPeriod lesson, string lastEnd)
    {
        if (lesson.TemplateGradeFrom <= 1 && lesson.LessonNumber == 2)
        {
            return new SlotSuggestion(
                BellPeriodKinds.DynamicPause,
                lesson.LessonNumber,
                lastEnd,
                BellTime.AddMinutes(lastEnd, DefaultDynamicPauseMinutes));
        }

        if (lesson.TemplateGradeFrom <= 4)
        {
            return new SlotSuggestion(
                BellPeriodKinds.Break,
                lesson.LessonNumber,
                lastEnd,
                BellTime.AddMinutes(lastEnd, DefaultBreakMinutes));
        }

        return new SlotSuggestion(
            BellPeriodKinds.Break,
            lesson.LessonNumber,
            lastEnd,
            BellTime.AddMinutes(lastEnd, DefaultBreakMinutes));
    }

    private static SlotSuggestion SuggestAfterDynamicPause(BellPeriod pause, string lastEnd) =>
        new(
            BellPeriodKinds.Lesson,
            pause.LessonNumber + 1,
            lastEnd,
            BellTime.AddMinutes(lastEnd, pause.TemplateGradeFrom <= 1 ? 35 : DefaultLessonMinutes));

    private static SlotSuggestion SuggestAfterBreak(BellPeriod breakPeriod, string lastEnd) =>
        new(
            BellPeriodKinds.Lesson,
            breakPeriod.LessonNumber + 1,
            lastEnd,
            BellTime.AddMinutes(lastEnd, breakPeriod.TemplateGradeFrom <= 1 ? 35 : DefaultLessonMinutes));
}
