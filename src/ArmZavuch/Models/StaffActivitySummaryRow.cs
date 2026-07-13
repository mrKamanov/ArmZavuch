namespace ArmZavuch.Models;

/// <summary>Сводка активности педагога за период: отсутствия и замены.</summary>
public sealed class StaffActivitySummaryRow
{
    public int TeacherId { get; init; }
    public string TeacherName { get; init; } = "";
    public int AbsenceDays { get; init; }
    public int SickDays { get; init; }
    public int LeaveDays { get; init; }
    public int OtherDays { get; init; }
    public int SubstitutionCount { get; init; }
    public int OfficialSubstitutionCount { get; init; }
    public int UnofficialSubstitutionCount { get; init; }
    public int WasReplacedCount { get; init; }

    public string AbsenceBreakdown =>
        AbsenceDays == 0
            ? "—"
            : $"бол. {SickDays} · отг. {LeaveDays} · проч. {OtherDays}";

    public string SubstitutionBreakdown =>
        SubstitutionCount == 0
            ? "—"
            : $"офиц. {OfficialSubstitutionCount} · неофиц. {UnofficialSubstitutionCount}";
}
