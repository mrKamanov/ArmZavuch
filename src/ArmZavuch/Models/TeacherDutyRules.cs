namespace ArmZavuch.Models;

/// <summary>Нормы рабочего дня сотрудников для подбора замен (ТЗ §5).</summary>
public static class TeacherDutyRules
{
    public static bool IsAuxiliary(string teacherType) =>
        string.Equals(teacherType, TeacherTypes.Auxiliary, StringComparison.Ordinal);
}
