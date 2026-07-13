namespace ArmZavuch.Models;

/// <summary>Режим очистки раздела «Нагрузка».</summary>
public enum CurriculumClearMode
{
    /// <summary>Часы по классам и предметам + назначения педагогов.</summary>
    All,
    /// <summary>Только привязки «кто ведёт»; строки нагрузки остаются.</summary>
    TeacherAssignmentsOnly
}
