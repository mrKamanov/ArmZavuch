namespace ArmZavuch.Models;

/// <summary>Куда вести пользователя по клику на замечание проверки норм.</summary>
public enum ComplianceNavigationTarget
{
    None,
    ScheduleGrid,
    DirectoriesClasses,
    DirectoriesTeachers,
    DirectoriesCurriculum
}
