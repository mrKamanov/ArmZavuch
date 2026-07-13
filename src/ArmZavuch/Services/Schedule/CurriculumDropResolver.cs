using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>Подбор учителя при перетаскивании предмета из нагрузки в сетку.</summary>
public static class CurriculumDropResolver
{
    private const int ClassSubjectExplicitScore = 220;
    private const int PrimarySubjectScore = 100;
    private const int SecondarySubjectScore = 80;
    private const int PreferredClassScore = 85;
    private const int HomeroomScore = 70;
    private const int PrimaryTeacherBonus = 20;
    private const int FirstGradeEligibleScore = 5;

    public static Teacher? ResolveTeacher(
        IEnumerable<Teacher> teachers,
        string subjectName,
        int classId,
        int classGrade,
        string classDisplayName,
        int subjectId = 0)
    {
        var list = teachers as IReadOnlyList<Teacher> ?? teachers.ToList();

        if (subjectId > 0)
        {
            var map = TeacherCurriculumMapBuilder.FromTeachers(list);
            var unique = TeacherCurriculumMapBuilder.UniqueTeacher(map, classId, subjectId, list);
            if (unique is not null)
                return unique;
        }

        Teacher? best = null;
        var bestScore = 0;

        foreach (var teacher in list)
        {
            var score = ScoreTeacher(teacher, subjectName, classId, classGrade, classDisplayName, subjectId);
            if (score < bestScore)
                continue;

            if (score == bestScore && best is not null)
            {
                var teacherBound = IsStronglyBound(teacher, classId, classDisplayName, subjectId);
                var bestBound = IsStronglyBound(best, classId, classDisplayName, subjectId);
                if (!teacherBound && bestBound)
                    continue;
            }

            if (score <= 0)
                continue;

            bestScore = score;
            best = teacher;
        }

        if (best is not null)
            return best;

        if (classGrade is >= 1 and <= 4)
        {
            var bound = list
                .Where(t => TeacherRecommendation.IsExplicitlyBoundToClass(t, classId, classDisplayName))
                .ToList();
            if (bound.Count == 1)
                return bound[0];
        }

        return null;
    }

    private static bool IsStronglyBound(Teacher teacher, int classId, string classDisplayName, int subjectId) =>
        subjectId > 0 && TeacherRecommendation.IsBoundToClassSubject(teacher, classId, subjectId)
        || TeacherRecommendation.IsExplicitlyBoundToClass(teacher, classId, classDisplayName);

    private static int ScoreTeacher(
        Teacher teacher, string subjectName, int classId, int classGrade, string classDisplayName, int subjectId)
    {
        var score = 0;

        if (subjectId > 0 && TeacherRecommendation.IsBoundToClassSubject(teacher, classId, subjectId))
            score += ClassSubjectExplicitScore;

        if (teacher.PrimarySubject?.Equals(subjectName, StringComparison.OrdinalIgnoreCase) == true)
            score += PrimarySubjectScore;
        else if (teacher.SecondarySubjects.Any(s =>
                     s.Equals(subjectName, StringComparison.OrdinalIgnoreCase)))
            score += SecondarySubjectScore;

        if (teacher.PreferredClassIds.Contains(classId))
            score += PreferredClassScore;
        else if (teacher.HomeroomClass?.Equals(classDisplayName, StringComparison.OrdinalIgnoreCase) == true)
            score += HomeroomScore;

        if (classGrade == 1 && teacher.WorksWithFirstGrade)
            score += FirstGradeEligibleScore;

        if (classGrade is >= 1 and <= 4 && teacher.TeacherType == TeacherTypes.Primary)
            score += PrimaryTeacherBonus;

        return score;
    }
}
