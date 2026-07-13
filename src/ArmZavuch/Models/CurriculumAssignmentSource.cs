namespace ArmZavuch.Models;

/// <summary>Источник явной привязки в анкете (нагрузка, классы).</summary>
public static class CurriculumAssignmentSource
{
    /// <summary>Анкета или таблица нагрузки — не снимается при правках в конструкторе.</summary>
    public const string Explicit = "explicit";

    /// <summary>Только из конструктора — снимается, если учитель убран из всех ячеек.</summary>
    public const string Schedule = "schedule";
}
