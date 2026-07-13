using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>Сводка явных привязок педагогов к строкам нагрузки.</summary>
public static class TeacherCurriculumMapBuilder
{
    public static Dictionary<(int ClassId, int SubjectId), HashSet<int>> FromTeachers(
        IReadOnlyList<Teacher> teachers,
        string? templateWeekParity = null)
    {
        var map = new Dictionary<(int, int), HashSet<int>>();
        foreach (var teacher in teachers)
        {
            foreach (var assignment in teacher.CurriculumAssignments)
            {
                if (templateWeekParity is not null
                    && !CurriculumWeekParity.MatchesForTemplate(assignment.WeekParity, templateWeekParity))
                    continue;

                var key = (assignment.ClassId, assignment.SubjectId);
                if (!map.TryGetValue(key, out var set))
                {
                    set = [];
                    map[key] = set;
                }

                set.Add(teacher.Id);
            }
        }

        return map;
    }

    public static Teacher? UniqueTeacher(
        IReadOnlyDictionary<(int ClassId, int SubjectId), HashSet<int>> map,
        int classId,
        int subjectId,
        IReadOnlyList<Teacher> teachers)
    {
        if (!map.TryGetValue((classId, subjectId), out var ids) || ids.Count != 1)
            return null;

        var teacherId = ids.First();
        return teachers.FirstOrDefault(t => t.Id == teacherId);
    }
}
