using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>Мягкие подсказки: каких учителей показывать первыми при наборе ячейки.</summary>
public static class TeacherRecommendation
{
    public const string ClassSubjectGroup = "★ Нагрузка для ячейки";
    public const string RecommendedGroup = "★ Для этого класса";
    public const string OtherGroup = "Остальные";

    public static bool IsBoundToClassSubject(Teacher teacher, int classId, int subjectId) =>
        subjectId > 0
        && TeacherCurriculumMatcher.TeacherHasAssignment(teacher, classId, subjectId);

    public static bool TeachesSubjectName(Teacher teacher, string? subjectName)
    {
        if (string.IsNullOrWhiteSpace(subjectName))
            return false;

        if (teacher.PrimarySubject?.Equals(subjectName, StringComparison.OrdinalIgnoreCase) == true)
            return true;

        return teacher.SecondarySubjects?.Any(s =>
            s.Equals(subjectName, StringComparison.OrdinalIgnoreCase)) == true;
    }

    /// <summary>Явная привязка: класс в анкете или классное руководство.</summary>
    public static bool IsExplicitlyBoundToClass(Teacher teacher, int classId, string classDisplayName)
    {
        if (teacher.PreferredClassIds?.Contains(classId) == true)
            return true;

        if (!string.IsNullOrWhiteSpace(teacher.HomeroomClass) &&
            teacher.HomeroomClass.Equals(classDisplayName, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public static bool IsRecommendedFor(Teacher teacher, int classId, int classGrade, string classDisplayName) =>
        IsExplicitlyBoundToClass(teacher, classId, classDisplayName);

    public static (List<Teacher> ClassSubject, List<Teacher> Recommended, List<Teacher> Others) SplitForCell(
        IEnumerable<Teacher> teachers,
        int classId,
        int classGrade,
        string classDisplayName,
        int subjectId,
        string? subjectName)
    {
        var classSubject = new List<Teacher>();
        var recommended = new List<Teacher>();
        var others = new List<Teacher>();

        foreach (var teacher in teachers.OrderBy(t => t.FullName, StringComparer.OrdinalIgnoreCase))
        {
            if (subjectId > 0 && IsBoundToClassSubject(teacher, classId, subjectId))
            {
                classSubject.Add(teacher);
                continue;
            }

            if (IsRecommendedFor(teacher, classId, classGrade, classDisplayName))
                recommended.Add(teacher);
            else
                others.Add(teacher);
        }

        if (classGrade == 1)
        {
            SortFirstGrade(others);
            SortFirstGrade(recommended);
        }

        return (classSubject, recommended, others);
    }

    public static (List<Teacher> Recommended, List<Teacher> Others) SplitForClass(
        IEnumerable<Teacher> teachers,
        int classId,
        int classGrade,
        string classDisplayName) =>
        SplitForCell(teachers, classId, classGrade, classDisplayName, 0, null) switch
        {
            var (classSubject, recommended, others) => (
                classSubject.Concat(recommended).ToList(),
                others)
        };

    private static void SortFirstGrade(List<Teacher> list)
    {
        list.Sort((a, b) =>
        {
            var byFirstGrade = a.WorksWithFirstGrade.CompareTo(b.WorksWithFirstGrade);
            if (byFirstGrade != 0)
                return -byFirstGrade;

            return string.Compare(a.FullName, b.FullName, StringComparison.OrdinalIgnoreCase);
        });
    }
}
