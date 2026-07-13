namespace ArmZavuch.Models;

/// <summary>Ячейка этапа 1: когда педагог занят (без класса, предмета и кабинета).</summary>
public sealed class TeacherTimeSlot
{
    public int TeacherId { get; init; }
    public string TeacherName { get; init; } = "";
    public int DayOfWeek { get; init; }
    public int LessonNumber { get; init; }
    public string? BuildingName { get; init; }
    /// <summary>Класс в шаблоне для просмотра в обзоре «Педагоги».</summary>
    public int? AnchorClassId { get; init; }
}
