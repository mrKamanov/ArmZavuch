namespace ArmZavuch.Models;

/// <summary>Строка выбора учителя в конструкторе (с группировкой подсказок).</summary>
public sealed class TeacherPickerItem
{
    public required Teacher Teacher { get; init; }
    public required string GroupName { get; init; }

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Teacher.HomeroomClass)
            ? Teacher.FullName
            : $"{Teacher.FullName} · кл.рук. {Teacher.HomeroomClass}";
}
