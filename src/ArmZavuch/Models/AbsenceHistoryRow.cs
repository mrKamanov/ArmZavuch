namespace ArmZavuch.Models;

/// <summary>Строка журнала отсутствий за выбранный период отчёта.</summary>
public sealed class AbsenceHistoryRow
{
    public int PeriodId { get; init; }
    public int TeacherId { get; init; }
    public string TeacherName { get; init; } = "";
    public string StatusType { get; init; } = StaffStatusTypes.Sick;
    public string StartDate { get; init; } = "";
    public string? EndDate { get; init; }
    public int DaysInRange { get; init; }
    public bool IsOfficial { get; init; }
    public string Source { get; init; } = "";
    public string? Note { get; init; }

    public string StatusLabel => StaffStatusTypes.ToDisplay(StatusType);

    public string PeriodDisplay => string.IsNullOrWhiteSpace(EndDate)
        ? $"{FormatDate(StartDate)} — …"
        : $"{FormatDate(StartDate)} — {FormatDate(EndDate)}";

    public string OfficialLabel => IsOfficial ? "официально" : "неофициально";

    private static string FormatDate(string iso) =>
        DateOnly.TryParse(iso, out var d) ? d.ToString("dd.MM.yyyy") : iso;
}
