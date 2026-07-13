using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>
/// Сколько часов недели уже разложено в шаблоне для пары класс+предмет.
/// Без подгрупп: уникальные слоты (день + урок), параллельные подгруппы в одном слоте — один час.
/// С подгруппами и двумя педагогами в нагрузке: максимум слотов среди назначенных педагогов.
/// С подгруппами и одним педагогом: максимум уникальных слотов по индексам подгрупп (ротация).
/// </summary>
public static class CurriculumScheduledHours
{
    public static double CountForClassSubject(
        IEnumerable<LessonSlot> slots,
        int classId,
        int subjectId,
        bool hasSubgroups = false,
        IReadOnlyList<int>? assignedTeacherIds = null)
    {
        var matched = slots
            .Where(s => s.ClassId == classId && s.SubjectId == subjectId)
            .ToList();

        if (matched.Count == 0)
            return 0;

        if (!hasSubgroups)
        {
            return matched
                .GroupBy(s => (s.DayOfWeek, s.LessonNumber))
                .Count();
        }

        var distinctTeachers = assignedTeacherIds?
            .Where(id => id > 0)
            .Distinct()
            .ToList() ?? [];

        if (distinctTeachers.Count >= 2)
        {
            return distinctTeachers
                .Select(tid => matched.Count(s => s.TeacherId == tid))
                .Max();
        }

        return matched
            .GroupBy(s => s.SubgroupIndex)
            .Select(g => g.GroupBy(s => (s.DayOfWeek, s.LessonNumber)).Count())
            .DefaultIfEmpty(0)
            .Max();
    }
}
