using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>
/// Хронология дня педагога для проверки переходов: штатные уроки, назначенные замены и предполагаемый слот.
/// Вход: уроки дня, id педагога, опционально предлагаемая замена. Выход: список слотов с TeacherId педагога.
/// </summary>
public static class TeacherDayTimelineBuilder
{
    public static List<LessonSlot> Build(
        IReadOnlyList<LessonSlot> dayLessons,
        int teacherId,
        LessonSlot? proposed = null)
    {
        var result = new List<LessonSlot>();

        foreach (var lesson in dayLessons.Where(l => !l.IsCancelled))
        {
            if (IsSameSlot(lesson, proposed))
                continue;

            if (lesson.TeacherId == teacherId)
            {
                result.Add(AsTeacherSlot(lesson, teacherId));
                continue;
            }

            if (lesson.HasAssignedReplacement && lesson.ReplacementTeacherId == teacherId)
                result.Add(AsTeacherSlot(lesson, teacherId));
        }

        if (proposed is not null)
            result.Add(AsTeacherSlot(proposed, teacherId));

        return result;
    }

    private static bool IsSameSlot(LessonSlot lesson, LessonSlot? proposed) =>
        proposed is not null
        && lesson.ClassId == proposed.ClassId
        && lesson.LessonNumber == proposed.LessonNumber
        && lesson.SubgroupIndex == proposed.SubgroupIndex;

    private static LessonSlot AsTeacherSlot(LessonSlot source, int teacherId) => new()
    {
        SlotId = source.SlotId,
        Date = source.Date,
        LessonNumber = source.LessonNumber,
        DisplayLessonNumber = source.DisplayLessonNumber,
        StartTime = source.StartTime,
        EndTime = source.EndTime,
        ClassId = source.ClassId,
        ClassName = source.ClassName,
        ClassGrade = source.ClassGrade,
        ClassShift = source.ClassShift,
        SubjectId = source.SubjectId,
        SubjectName = source.SubjectName,
        TeacherId = teacherId,
        TeacherName = source.TeacherName,
        RoomId = source.RoomId,
        RoomNumber = source.RoomNumber,
        BuildingName = source.BuildingName,
        SubgroupIndex = source.SubgroupIndex,
        DayOfWeek = source.DayOfWeek,
        IsCancelled = false
    };
}
