namespace ArmZavuch.Models;

/// <summary>Период отсутствия сотрудника: с даты, опционально по дату (null = открытый).</summary>
public sealed class TeacherStatusPeriod
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public string StatusType { get; set; } = StaffStatusTypes.Sick;
    public string StartDate { get; set; } = "";
    public string? EndDate { get; set; }
    public string? Note { get; set; }
    public bool IsOfficial { get; set; }
    public string Source { get; set; } = AbsenceSources.Profile;

    public bool IsOpen => string.IsNullOrWhiteSpace(EndDate);

    public string AbsenceNoteText =>
        string.IsNullOrWhiteSpace(Note)
            ? StaffStatusTypes.ToDisplay(StatusType)
            : $"{StaffStatusTypes.ToDisplay(StatusType)}: {Note}";

    public string DateRangeDisplay => IsOpen
        ? $"{FormatDate(StartDate)} — …"
        : StartDate == EndDate
            ? FormatDate(StartDate)
            : $"{FormatDate(StartDate)} — {FormatDate(EndDate!)}";

    public string DisplayText =>
        $"{StaffStatusTypes.ToIcon(AbsenceNoteText)}{StaffStatusTypes.ToDisplay(StatusType)}: {DateRangeDisplay}" +
        (string.IsNullOrWhiteSpace(Note) ? "" : $" ({Note})") +
        (IsOfficial ? " · официально" : "");

    private static string FormatDate(string iso) =>
        DateOnly.TryParse(iso, out var d) ? d.ToString("dd.MM.yyyy") : iso;
}
