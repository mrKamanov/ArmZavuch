using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>
/// Определяет, закончил ли учитель уроки до заданного слота замены.
/// Сравнение по времени (не по номеру урока); вспомогательные не уходят в нижний список.
/// </summary>
public static class TeacherDayFinishedDetector
{
    private const int MinLessonsPassedForFinishedDay = 1;

    public sealed record Result(
        bool IsDayFinished,
        string? LastLessonEndTime,
        string? StillWorkingHint = null);

    public static Result Evaluate(
        Teacher teacher,
        LessonSlot slot,
        IReadOnlyList<LessonSlot> dayLessons,
        IReadOnlyList<BellPeriod> bellPeriods)
    {
        var teacherLessons = dayLessons
            .Where(lesson => !lesson.IsCancelled && OccupiesTeacher(lesson, teacher.Id))
            .ToList();

        if (teacherLessons.Count == 0)
            return new Result(false, null);

        var slotStart = BellScheduleResolver.ResolveLessonStartTime(slot, bellPeriods);
        var laterLesson = FindNextLessonAtOrAfter(teacherLessons, slot, bellPeriods, slotStart);
        var lastLesson = FindLastLessonEndingBefore(teacherLessons, slot, bellPeriods, slotStart);
        var lastLessonEndTime = lastLesson is null
            ? null
            : FormatClock(BellScheduleResolver.ResolveLessonEndTime(lastLesson, bellPeriods));

        if (TeacherDutyRules.IsAuxiliary(teacher.TeacherType))
        {
            return new Result(
                false,
                lastLessonEndTime,
                laterLesson is null ? null : FormatStillWorkingHint(laterLesson, bellPeriods));
        }

        if (laterLesson is not null)
        {
            return new Result(
                false,
                lastLessonEndTime,
                StillWorkingHint: FormatStillWorkingHint(laterLesson, bellPeriods));
        }

        if (lastLesson is null)
            return new Result(false, null);

        if (!HasLongGapAfterLastLesson(lastLesson, slot, bellPeriods, slotStart))
            return new Result(false, lastLessonEndTime);

        return new Result(true, lastLessonEndTime);
    }

    private static LessonSlot? FindLastLessonEndingBefore(
        IReadOnlyList<LessonSlot> teacherLessons,
        LessonSlot slot,
        IReadOnlyList<BellPeriod> bellPeriods,
        TimeSpan? slotStart)
    {
        LessonSlot? best = null;
        TimeSpan? bestEnd = null;

        foreach (var lesson in teacherLessons)
        {
            var lessonEnd = BellScheduleResolver.ResolveLessonEndTime(lesson, bellPeriods);
            if (lessonEnd is null || slotStart is null)
                continue;
            if (lessonEnd.Value > slotStart.Value)
                continue;

            if (bestEnd is null || lessonEnd.Value > bestEnd.Value)
            {
                best = lesson;
                bestEnd = lessonEnd;
            }
        }

        if (best is not null)
            return best;

        return teacherLessons
            .Where(lesson => lesson.LessonNumber < slot.LessonNumber)
            .OrderByDescending(lesson => lesson.LessonNumber)
            .FirstOrDefault();
    }

    private static LessonSlot? FindNextLessonAtOrAfter(
        IReadOnlyList<LessonSlot> teacherLessons,
        LessonSlot slot,
        IReadOnlyList<BellPeriod> bellPeriods,
        TimeSpan? slotStart)
    {
        LessonSlot? best = null;
        TimeSpan? bestStart = null;

        foreach (var lesson in teacherLessons)
        {
            var lessonStart = BellScheduleResolver.ResolveLessonStartTime(lesson, bellPeriods);
            if (lessonStart is null || slotStart is null)
                continue;
            if (lessonStart.Value < slotStart.Value)
                continue;

            if (bestStart is null || lessonStart.Value < bestStart.Value)
            {
                best = lesson;
                bestStart = lessonStart;
            }
        }

        if (best is not null)
            return best;

        return teacherLessons
            .Where(lesson => lesson.LessonNumber >= slot.LessonNumber)
            .OrderBy(lesson => lesson.LessonNumber)
            .FirstOrDefault();
    }

    private static bool HasLongGapAfterLastLesson(
        LessonSlot lastLesson,
        LessonSlot slot,
        IReadOnlyList<BellPeriod> bellPeriods,
        TimeSpan? slotStart)
    {
        var lastEnd = BellScheduleResolver.ResolveLessonEndTime(lastLesson, bellPeriods);
        if (lastEnd is not null && slotStart is not null)
        {
            var lessonsPassed = BellScheduleResolver.CountLessonsStartingBetweenForShift(
                bellPeriods,
                slot.ClassShift,
                lastEnd.Value,
                slotStart.Value);
            return lessonsPassed > MinLessonsPassedForFinishedDay;
        }

        return slot.LessonNumber - lastLesson.LessonNumber > MinLessonsPassedForFinishedDay;
    }

    private static bool OccupiesTeacher(LessonSlot lesson, int teacherId) =>
        lesson.TeacherId == teacherId
        || (lesson.HasAssignedReplacement && lesson.ReplacementTeacherId == teacherId);

    private static string FormatStillWorkingHint(LessonSlot lesson, IReadOnlyList<BellPeriod> bellPeriods)
    {
        var start = BellScheduleResolver.ResolveLessonStartTime(lesson, bellPeriods);
        return start is null
            ? $"Ещё урок №{lesson.LessonNumber}"
            : $"Ещё урок в {FormatClock(start)}";
    }

    private static string? FormatClock(TimeSpan? time) =>
        time is null ? null : TimeOnly.FromTimeSpan(time.Value).ToString("HH:mm");
}
