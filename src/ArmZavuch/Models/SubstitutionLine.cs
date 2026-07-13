namespace ArmZavuch.Models;

/// <summary>Строка листа замен для экспорта (ТЗ §7).</summary>
public sealed class SubstitutionLine
{
    public int LessonNumber { get; init; }
    public string DisplayLessonLabel { get; init; } = "";
    public string Time { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string SubjectName { get; init; } = "";
    public string OriginalTeacher { get; init; } = "";
    public string ReplacementTeacher { get; init; } = "";
    public string RoomNumber { get; init; } = "";
    public string BuildingName { get; init; } = "";
    public int ClassShift { get; init; } = 1;
    public bool IsPending { get; init; }
    public bool IsCancelled { get; init; }

    public SubstitutionExportKind ExportKind => IsCancelled
        ? SubstitutionExportKind.Cancelled
        : IsPending
            ? SubstitutionExportKind.Pending
            : SubstitutionExportKind.Assigned;

    public string LessonTitle =>
        string.IsNullOrWhiteSpace(DisplayLessonLabel)
            ? $"{LessonNumber} урок"
            : DisplayLessonLabel;

    public string RoomDisplay =>
        string.IsNullOrWhiteSpace(RoomNumber)
            ? ""
            : string.IsNullOrWhiteSpace(BuildingName)
                ? $"каб. {RoomNumber}"
                : $"каб. {RoomNumber} · {BuildingName}";
}
