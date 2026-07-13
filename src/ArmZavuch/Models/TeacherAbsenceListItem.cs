namespace ArmZavuch.Models;

/// <summary>Строка монитора отсутствующих педагогов на выбранную дату.</summary>
public sealed class TeacherAbsenceListItem
{
    public int PeriodId { get; init; }
    public int TeacherId { get; init; }
    public string TeacherName { get; init; } = "";
    public string StatusLabel { get; init; } = "";
    public string PeriodText { get; init; } = "";
    public string SourceText { get; init; } = "";
    public bool IsOfficial { get; init; }
    public bool IsOpen { get; init; }
}
