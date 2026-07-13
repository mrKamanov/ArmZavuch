namespace ArmZavuch.Services.Staff;

/// <summary>Лимиты назначения педагогов на строку нагрузки (п/г — до двух).</summary>
public static class CurriculumTeacherAssignmentRules
{
    public const int MaxWithoutSubgroups = 1;
    public const int MaxWithSubgroups = 2;

    public static int MaxTeachers(bool hasSubgroups) =>
        hasSubgroups ? MaxWithSubgroups : MaxWithoutSubgroups;

    public static bool IsValidCount(bool hasSubgroups, int count) =>
        count >= 0 && count <= MaxTeachers(hasSubgroups);

    public static List<int> Normalize(bool hasSubgroups, IEnumerable<int> teacherIds)
    {
        var list = teacherIds.Where(id => id > 0).Distinct().ToList();
        if (!IsValidCount(hasSubgroups, list.Count))
            list = list.Take(MaxTeachers(hasSubgroups)).ToList();
        return list;
    }
}
